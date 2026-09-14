-- =====================================================================
-- BlockingMonitor :: Master Query
-- تمام منطق تشخیص Head Blocker، عمق زنجیره، و self-blocking اینجا انجام میشه.
-- C# فقط این جدول Flat رو می‌خونه و بر اساس parent_session_id به Tree تبدیلش می‌کنه.
-- هر تغییری در منطق تشخیص بلاکینگ، فقط همینجا اعمال میشه؛ کد C# دست‌نخورده می‌مونه.
-- =====================================================================

;WITH Edges AS (
    SELECT DISTINCT
        wt.session_id,
        wt.blocking_session_id,
        wt.wait_type,
        wt.wait_duration_ms,
        wt.resource_description
    FROM sys.dm_os_waiting_tasks AS wt
    WHERE wt.blocking_session_id <> 0
      AND wt.wait_type LIKE 'LCK%'
),
Chain AS (
    -- Anchor: هر یال مستقیم، عمق 1
    SELECT
        e.session_id           AS origin_session_id,
        e.session_id           AS current_session_id,
        e.blocking_session_id  AS parent_session_id,
        1                      AS depth
    FROM Edges AS e

    UNION ALL

    -- Recursive: دنبال کردن زنجیره به سمت ریشه
    SELECT
        c.origin_session_id,
        e.session_id,
        e.blocking_session_id,
        c.depth + 1
    FROM Chain AS c
    JOIN Edges AS e ON e.session_id = c.parent_session_id
    WHERE c.depth < 50   -- safety guard در برابر چرخه‌ی غیرمنتظره
),
HeadBlockerPerOrigin AS (
    -- برای هر origin، عمیق‌ترین لایه = ریشه‌ی واقعی زنجیره (کسی که خودش بلاک نشده)
    SELECT
        origin_session_id,
        parent_session_id AS head_blocker_session_id,
        depth AS max_depth
    FROM Chain AS c1
    WHERE depth = (SELECT MAX(depth) FROM Chain AS c2 WHERE c2.origin_session_id = c1.origin_session_id)
),
SessionRoles AS (
    -- نقش هر session بلاک‌شده: پدرش کیه، عمقش چقدره، ریشه‌ی زنجیره‌ش کدومه
    SELECT DISTINCT
        e.session_id,
        e.blocking_session_id                                  AS parent_session_id,
        hb.head_blocker_session_id,
        hb.max_depth                                            AS depth,
        e.wait_type,
        e.wait_duration_ms,
        e.resource_description
    FROM Edges AS e
    LEFT JOIN HeadBlockerPerOrigin AS hb ON hb.origin_session_id = e.session_id

    UNION

    -- خودِ Head Blockerها هم باید یک ردیف داشته باشن، چون هیچ‌وقت طرف چپ Edges نمیان
    -- (چون خودشون منتظر چیزی نیستن -- سناریوی "Head Blocker خاموش")
    SELECT DISTINCT
        hb.head_blocker_session_id                             AS session_id,
        NULL                                                    AS parent_session_id,
        hb.head_blocker_session_id,
        0                                                       AS depth,
        NULL, NULL, NULL
    FROM HeadBlockerPerOrigin AS hb
)
SELECT
    s.session_id,
    s.status,
    r.command,
    COALESCE(r.wait_type, sr.wait_type)                         AS wait_type,
    sr.wait_duration_ms,
    sr.resource_description,
    tl.resource_type,
    -- نکته: resource_associated_entity_id برای OBJECT همون object_id هست،
    -- ولی برای PAGE/KEY/RID/HOBT در واقع hobt_id هست (نه object_id) و باید
    -- از طریق sys.partitions به object_id واقعی resolve بشه؛ در غیر این
    -- صورت OBJECT_NAME() یا NULL برمی‌گردونه یا اسم اشتباه.
    --CASE
    --    WHEN tl.resource_type = 'OBJECT'
    --         AND tl.resource_associated_entity_id BETWEEN 0 AND 2147483647
    --    THEN OBJECT_NAME(CONVERT(int, tl.resource_associated_entity_id), tl.resource_database_id)

    --    WHEN tl.resource_type IN ('PAGE','KEY','RID','HOBT')
    --    THEN OBJECT_NAME(p.object_id, tl.resource_database_id)

    --    ELSE NULL
    --END                                                          AS locked_object_name,

    sr.parent_session_id,
    sr.head_blocker_session_id,
    sr.depth,
    CASE WHEN sr.session_id = sr.head_blocker_session_id THEN 1 ELSE 0 END AS is_head_blocker,
    CASE WHEN sr.session_id = sr.parent_session_id THEN 1 ELSE 0 END       AS is_self_blocking,

    DB_NAME(COALESCE(r.database_id, s.database_id))            AS database_name,
    s.login_name,
    s.host_name,
    s.program_name,
    s.open_transaction_count,
    at.transaction_begin_time,
    DATEDIFF_BIG(SECOND, at.transaction_begin_time, GETDATE()) AS transaction_age_seconds,
    CASE at.transaction_type
        WHEN 1 THEN 'Read/Write' WHEN 2 THEN 'Read-Only'
        WHEN 3 THEN 'System' WHEN 4 THEN 'Distributed'
    END                                                          AS transaction_type,
    s.last_request_start_time,
    s.last_request_end_time,
    DATEDIFF_BIG(SECOND, s.last_request_end_time, GETDATE())    AS idle_seconds,
    r.total_elapsed_time,
    r.cpu_time,
    r.reads,
    r.writes,
    r.percent_complete,
    txt.text                                                    AS sql_text

FROM SessionRoles AS sr
JOIN sys.dm_exec_sessions AS s ON s.session_id = sr.session_id
LEFT JOIN sys.dm_exec_requests AS r ON r.session_id = s.session_id
LEFT JOIN sys.dm_exec_connections AS c ON c.session_id = s.session_id
LEFT JOIN sys.dm_tran_session_transactions AS tst ON tst.session_id = s.session_id
LEFT JOIN sys.dm_tran_active_transactions AS at ON at.transaction_id = tst.transaction_id
-- محدودیت مهم: sys.partitions فقط پارتیشن‌های دیتابیس جاری (همون که این
-- Connection بهش وصله) رو نشون می‌ده. اگه لاک روی یک دیتابیس دیگه باشه،
-- این JOIN چیزی پیدا نمی‌کنه و locked_object_name برای PAGE/KEY/RID/HOBT
-- در اون حالت NULL می‌مونه. رفعش نیاز به Dynamic SQL چند-دیتابیسه که
-- عمداً از دامنه‌ی این کوئری بیرون گذاشته شده.
--LEFT JOIN sys.partitions AS p
--    ON p.hobt_id = tl.resource_associated_entity_id
--    AND tl.resource_database_id = DB_ID()
LEFT JOIN sys.dm_tran_locks AS tl ON tl.request_session_id = s.session_id AND tl.request_status = 'WAIT'
OUTER APPLY sys.dm_exec_sql_text(COALESCE(r.sql_handle, c.most_recent_sql_handle)) txt
ORDER BY sr.head_blocker_session_id, sr.depth DESC;
