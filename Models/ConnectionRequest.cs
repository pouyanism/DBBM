using System.ComponentModel.DataAnnotations;

namespace BlockingMonitor.Models
{
    public class ConnectionRequest
    {
        [Required]
        public string ServerName { get; set; }

        [Required]
        public string DatabaseName { get; set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public bool UseWindowsAuth { get; set; }

        public int? Timeout { get; set; }
    }
}
