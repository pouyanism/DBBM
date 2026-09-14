using System.ComponentModel.DataAnnotations;

namespace BlockingMonitor.Models
{
    public class ConnectionViewModel
    {
        [Display(Name = "Server Name")]
        [Required(ErrorMessage = "Server name is required")]
        public string ServerName { get; set; }

        [Display(Name = "Database Name")]
        [Required(ErrorMessage = "Database name is required")]
        public string DatabaseName { get; set; }

        [Display(Name = "Username")]
        public string? Username { get; set; }

        [Display(Name = "Password")]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [Display(Name = "Use Windows Authentication")]
        public bool UseWindowsAuth { get; set; } = true;

        [Display(Name = "Connection Timeout (seconds)")]
        [Range(5, 300)]
        public int Timeout { get; set; } = 30;

        // برای نمایش وضعیت اتصال
        public bool IsConnected { get; set; }
        public string? ConnectionStatus { get; set; }
        public string? ActiveServerName { get; set; }
    }
}
