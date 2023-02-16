using System.ComponentModel.DataAnnotations;

namespace CloudGaming.Models;

public class VirtualM
{
    [Key]
    [Required]
    public string Name { get; set; }
    public string PublicIp { get; set; }
    [Required]public string Login { get; set; }
    [DataType(DataType.Password)]
    [Required]public string Password { get; set; }
}