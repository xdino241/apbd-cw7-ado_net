using apbd_cw7_ado.Models;

namespace apbd_cw7_ado.Services;

public interface IAppointmentServices
{
    public Task<IEnumerable<AppointmentListDto>> GetAllAppointmentsAsync(string? status, string? patientLastName);
    public Task<AppointmentDetailsDto?> GetAppointmentDetailsAsync(int idAppointment);
    public Task<int> UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto dto);
    public Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto dto);
    public Task<int> DeleteAppointmentAsync(int idAppointment);
}