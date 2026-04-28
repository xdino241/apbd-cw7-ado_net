using apbd_cw7_ado.Models;
using apbd_cw7_ado.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace apbd_cw7_ado.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AppointmentsController : ControllerBase
    {
        
        private readonly IAppointmentServices _appointmentService;
        public AppointmentsController(IAppointmentServices appointmentService)
        {
            _appointmentService = appointmentService;
        }
        
        [HttpGet]
        public async Task<IActionResult> GetAppointmentsAsync([FromQuery] string? status = null, [FromQuery] string? patientLastName = null)
        {
            var appointments = await _appointmentService.GetAllAppointmentsAsync(status, patientLastName);
            return Ok(appointments);
        }

        [HttpGet("{idAppointment}")]
        public async Task<IActionResult> GetAppointmentDetailsAsync(int idAppointment)
        {
            var appointment = await _appointmentService.GetAppointmentDetailsAsync(idAppointment);
            if (appointment == null)
            {
                return NotFound(new ErrorResponseDto { StatusCode = 404, Message = "Brak wizyty o podanym id" });
            }
            return Ok(appointment);
        }

        [HttpPut("{idAppointment}")]
        public async Task<IActionResult> UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto dto)
        {
            try 
            {
                var result = await _appointmentService.UpdateAppointmentAsync(idAppointment, dto);
                if (result == 0)
                {
                    return NotFound(new ErrorResponseDto { StatusCode = 404, Message = "Nie ma takiej wizyty" });
                }
                return Ok(result); 
            }
            catch (Exception ex)
            {
                return Conflict(new ErrorResponseDto { StatusCode = 409, Message = ex.Message });
            }
        }
        
        [HttpDelete("{idAppointment}")]
        public async Task<IActionResult> DeleteAppointmentAsync(int idAppointment)
        {
            try
            {
                var result = await _appointmentService.DeleteAppointmentAsync(idAppointment);
                if (result == 0)
                {
                    return NotFound(new ErrorResponseDto {StatusCode = 404,Message = $"Wizyta o ID {idAppointment} nie istnieje." });
                }

                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ErrorResponseDto { StatusCode = 409, Message = ex.Message });
            }
        }
        
        [HttpPost]
        public async Task<IActionResult> CreateAppointmentAsync([FromBody] CreateAppointmentRequestDto dto)
        {
            try
            {
                var newId = await _appointmentService.CreateAppointmentAsync(dto);
                return Created($"/api/appointments/{newId}", new { IdAppointment = newId });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ErrorResponseDto { StatusCode = 400, Message = ex.Message });
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("nie istnieje"))
                {
                    return NotFound(new ErrorResponseDto { StatusCode = 404, Message = ex.Message });
                }
                return Conflict(new ErrorResponseDto{ StatusCode = 409, Message = ex.Message });
            }
        }
    }
}
