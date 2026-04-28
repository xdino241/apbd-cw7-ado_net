using apbd_cw7_ado.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.SqlClient;

namespace apbd_cw7_ado.Services;

public class AppointmentService : IAppointmentServices
{
    private readonly string _connectionString;

    public AppointmentService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection");
    }

    public async Task<IEnumerable<AppointmentListDto>> GetAllAppointmentsAsync(string? status, string? patientLastName)
    {
        var appointmentsList = new List<AppointmentListDto>();
        
        var query = @"
        SELECT a.IdAppointment, a.AppointmentDate, a.Status, a.Reason, p.FirstName + + p.LastName AS PatientFullName, p.Email AS PatientEmail
        FROM dbo.Appointments a
        JOIN dbo.Patients p ON p.IdPatient = a.IdPatient
        WHERE (@Status IS NULL OR a.Status = @Status)
          AND (@PatientLastName IS NULL OR p.LastName = @PatientLastName)
        ORDER BY a.AppointmentDate;";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(query, connection);
        
        command.Parameters.Add("@Status", System.Data.SqlDbType.NVarChar).Value = (object?)status ?? DBNull.Value;
        command.Parameters.Add("@PatientLastName", System.Data.SqlDbType.NVarChar).Value = (object?)patientLastName ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            appointmentsList.Add(new AppointmentListDto
            {
                IdAppointment = reader.GetInt32(0),
                AppointmentDate = reader.GetDateTime(1),
                Status = reader.GetString(2),
                Reason = reader.GetString(3),
                PatientFullName = reader.GetString(4),
                PatientEmail = reader.GetString(5)
            });
        }
        return appointmentsList;
    }

    public async Task<AppointmentDetailsDto?> GetAppointmentDetailsAsync(int idAppointment)
    {
        var query = @"
        SELECT a.IdAppointment, a.AppointmentDate, a.Status, a.Reason, a.InternalNotes, a.CreatedAt,
               p.FirstName + ' ' + p.LastName AS PatientFullName, p.Email, p.PhoneNumber,
               d.FirstName + ' ' + d.LastName AS DoctorFullName, d.LicenseNumber,
               s.Name AS DoctorSpecialization
        FROM dbo.Appointments a
        JOIN dbo.Patients p ON a.IdPatient = p.IdPatient
        JOIN dbo.Doctors d ON a.IdDoctor = d.IdDoctor
        JOIN dbo.Specializations s ON d.IdSpecialization = s.IdSpecialization
        WHERE a.IdAppointment = @IdAppointment";
        
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(query, connection);
        
        command.Parameters.Add("@IdAppointment", System.Data.SqlDbType.Int).Value = idAppointment;
        
        await using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            return new AppointmentDetailsDto
            {
                IdAppointment = reader.GetInt32(0),
                AppointmentDate = reader.GetDateTime(1),
                Status = reader.GetString(2),
                Reason = reader.GetString(3),
                InternalNotes = reader.IsDBNull(4) ? null : reader.GetString(4),
                CreatedAt = reader.GetDateTime(5),
                PatientFullName = reader.GetString(6),
                PatientEmail = reader.GetString(7),
                PatientPhoneNumber = reader.GetString(8),
                DoctorFullName = reader.GetString(9),
                DoctorLicenseNumber = reader.GetString(10),
                DoctorSpecialization = reader.GetString(11)
            };
        }
        return null;
    }

    public async Task<int> UpdateAppointmentAsync(int idAppointment, UpdateAppointmentRequestDto dto)
    {
        var query = @"
        UPDATE Appointments 
        SET IdPatient = @IdPatient, 
            IdDoctor = @IdDoctor, 
            AppointmentDate = @AppointmentDate, 
            Status = @Status, 
            Reason = @Reason, 
            InternalNotes = @InternalNotes
        WHERE IdAppointment = @Id";

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(query, connection);
    
        command.Parameters.AddWithValue("@Id", idAppointment);
        command.Parameters.AddWithValue("@IdPatient", dto.IdPatient);
        command.Parameters.AddWithValue("@IdDoctor", dto.IdDoctor);
        command.Parameters.AddWithValue("@AppointmentDate", dto.AppointmentDate);
        command.Parameters.AddWithValue("@Status", dto.Status);
        command.Parameters.AddWithValue("@Reason", dto.Reason);
        command.Parameters.AddWithValue("@InternalNotes", (object?)dto.InternalNotes ?? DBNull.Value);
        
        return await command.ExecuteNonQueryAsync();
    }

    public async Task<int> CreateAppointmentAsync(CreateAppointmentRequestDto dto)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        if (dto.AppointmentDate < DateTime.Now)
        {
            throw new ArgumentException("Termin wizyty nie może być w przeszłości.");
        }
        
        var doctorCheckQuery = "SELECT IsActive FROM Doctors WHERE IdDoctor = @IdDoctor";
        await using var doctorCommand = new SqlCommand(doctorCheckQuery, connection);
        doctorCommand.Parameters.Add("@IdDoctor", System.Data.SqlDbType.Int).Value = dto.IdDoctor;
        var isDoctorActive = await doctorCommand.ExecuteScalarAsync();
        if (isDoctorActive == null)  
        {
            throw new Exception("Doktor nie istnieje");
        }
        if (!(bool)isDoctorActive)
        {
            throw new Exception("Doktor jest nieaktywny");
        }
        
        var patientCheckQuery = "SELECT IsActive FROM Patients WHERE IdPatient = @IdPatient";
        await using var patientCommand = new SqlCommand(patientCheckQuery, connection);
        patientCommand.Parameters.Add("@IdPatient", System.Data.SqlDbType.Int).Value = dto.IdPatient;
        
        var isPatientActive = await patientCommand.ExecuteScalarAsync();
        if (isPatientActive == null)  
        {
            throw new Exception("Pacjent nie istnieje");
        }
        if (!(bool)isPatientActive)
        {
            throw new Exception("Pacjent jest nieaktywny");
        }
        
        var conflictCheckQuery = @"
        SELECT 1 
        FROM Appointments 
        WHERE IdDoctor = @IdDoctor 
        AND AppointmentDate = @AppointmentDate 
        AND Status <> 'Cancelled'";
          
        await using var conflictCommand = new SqlCommand(conflictCheckQuery, connection);
        conflictCommand.Parameters.Add("@IdDoctor", System.Data.SqlDbType.Int).Value = dto.IdDoctor;
        conflictCommand.Parameters.Add("@AppointmentDate", System.Data.SqlDbType.DateTime2).Value = dto.AppointmentDate;
    
        var result = await conflictCommand.ExecuteScalarAsync();
        if (result != null)
        {
            throw new Exception("Doktor ma już zaplanowaną wizytę w tym terminie");
        }
        
        var insertQuery = @"
        INSERT INTO Appointments (IdPatient, IdDoctor, AppointmentDate, Status, Reason)
        VALUES (@IdPatient, @IdDoctor, @AppointmentDate, 'Scheduled', @Reason);
        SELECT CAST(SCOPE_IDENTITY() AS INT);";
        await using var insertCommand = new SqlCommand(insertQuery, connection);
        insertCommand.Parameters.Add("@IdPatient", System.Data.SqlDbType.Int).Value = dto.IdPatient;
        insertCommand.Parameters.Add("@IdDoctor", System.Data.SqlDbType.Int).Value = dto.IdDoctor;
        insertCommand.Parameters.Add("@AppointmentDate", System.Data.SqlDbType.DateTime2).Value = dto.AppointmentDate;
        insertCommand.Parameters.Add("@Reason", System.Data.SqlDbType.NVarChar).Value = dto.Reason;
        
        var newId = (int) await insertCommand.ExecuteScalarAsync();
        return newId;
    }

    public async Task<int> DeleteAppointmentAsync(int idAppointment)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        
        var query = "SELECT Status FROM Appointments WHERE IdAppointment = @IdAppointment";
        await using var command = new SqlCommand(query, connection);
        command.Parameters.Add("@IdAppointment", System.Data.SqlDbType.Int).Value = idAppointment;
        var res = await command.ExecuteScalarAsync();

        if (res == null)
        {
            return 0;
        }

        var status = res.ToString();
        if (status == "Completed")
        {
            throw new InvalidOperationException("Nie mozna usunac Appointmentu ktory ma status Completed");
        }
        var delete = "DELETE FROM Appointments WHERE IdAppointment = @IdAppointment";
        await using var delcommand = new SqlCommand(delete, connection);
        delcommand.Parameters.Add("@IdAppointment", System.Data.SqlDbType.Int).Value = idAppointment;
        
        await delcommand.ExecuteNonQueryAsync();
        return 1;
    }
}