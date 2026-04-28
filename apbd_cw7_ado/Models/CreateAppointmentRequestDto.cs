using System.ComponentModel.DataAnnotations;

namespace apbd_cw7_ado.Models;

public class CreateAppointmentRequestDto
{
    [Required]
    public int IdPatient { get; set; }

    [Required]
    public int IdDoctor { get; set; }

    [Required]
    public DateTime AppointmentDate { get; set; }

    [Required]
    [MaxLength(250)]
    public string Reason { get; set; }
}