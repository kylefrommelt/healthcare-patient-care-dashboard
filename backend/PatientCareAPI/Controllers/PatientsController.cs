using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PatientCareAPI.Data;
using PatientCareAPI.Models;
using PatientCareAPI.DTOs;
using PatientCareAPI.Services;
using AutoMapper;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
using System.ComponentModel.DataAnnotations;

namespace PatientCareAPI.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    [Produces("application/json")]
    public class PatientsController : ControllerBase
    {
        private readonly PatientCareDbContext _context;
        private readonly IPatientService _patientService;
        private readonly IMapper _mapper;
        private readonly ILogger<PatientsController> _logger;
        private readonly IDataProtector _dataProtector;
        private readonly IAuditService _auditService;

        public PatientsController(
            PatientCareDbContext context,
            IPatientService patientService,
            IMapper mapper,
            ILogger<PatientsController> logger,
            IDataProtectionProvider dataProtectionProvider,
            IAuditService auditService)
        {
            _context = context;
            _patientService = patientService;
            _mapper = mapper;
            _logger = logger;
            _dataProtector = dataProtectionProvider.CreateProtector("PatientData");
            _auditService = auditService;
        }

        /// <summary>
        /// Get all patients with pagination and filtering
        /// </summary>
        /// <param name="page">Page number (default: 1)</param>
        /// <param name="pageSize">Items per page (default: 10, max: 100)</param>
        /// <param name="search">Search term for patient name or MRN</param>
        /// <param name="status">Filter by patient status</param>
        /// <param name="assignedPhysician">Filter by assigned physician</param>
        /// <returns>Paginated list of patients</returns>
        [HttpGet]
        [Authorize(Roles = "Admin,Physician,Nurse,Receptionist")]
        [ProducesResponseType(typeof(PatientListResponseDto), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<PatientListResponseDto>> GetPatients(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] PatientStatus? status = null,
            [FromQuery] string? assignedPhysician = null)
        {
            try
            {
                // Validate pagination parameters
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                // Log access attempt
                await _auditService.LogAccessAsync(
                    currentUserId,
                    "Patient",
                    "List",
                    $"Page: {page}, PageSize: {pageSize}, Search: {search}"
                );

                var result = await _patientService.GetPatientsAsync(
                    page, pageSize, search, status, assignedPhysician, currentUserRole, currentUserId);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving patients");
                return StatusCode(500, "An error occurred while retrieving patients");
            }
        }

        /// <summary>
        /// Get a specific patient by ID
        /// </summary>
        /// <param name="id">Patient ID</param>
        /// <returns>Patient details</returns>
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "Admin,Physician,Nurse,Receptionist,Technician")]
        [ProducesResponseType(typeof(PatientDetailResponseDto), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<PatientDetailResponseDto>> GetPatient(Guid id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                // Check if user has permission to view this patient
                if (!await _patientService.CanAccessPatientAsync(id, currentUserId, currentUserRole))
                {
                    return Forbid("You do not have permission to access this patient's information");
                }

                var patient = await _patientService.GetPatientDetailAsync(id);
                
                if (patient == null)
                {
                    return NotFound($"Patient with ID {id} not found");
                }

                // Log access
                await _auditService.LogAccessAsync(
                    currentUserId,
                    "Patient",
                    "View",
                    $"PatientId: {id}"
                );

                return Ok(patient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving patient {PatientId}", id);
                return StatusCode(500, "An error occurred while retrieving the patient");
            }
        }

        /// <summary>
        /// Create a new patient
        /// </summary>
        /// <param name="patientDto">Patient data</param>
        /// <returns>Created patient</returns>
        [HttpPost]
        [Authorize(Roles = "Admin,Physician,Nurse,Receptionist")]
        [ProducesResponseType(typeof(PatientResponseDto), 201)]
        [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<PatientResponseDto>> CreatePatient(
            [FromBody] PatientCreateDto patientDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUserId = GetCurrentUserId();

                // Validate business rules
                var validationResult = await _patientService.ValidatePatientCreateAsync(patientDto);
                if (!validationResult.IsValid)
                {
                    return BadRequest(validationResult.Errors);
                }

                var patient = await _patientService.CreatePatientAsync(patientDto, currentUserId);

                // Log creation
                await _auditService.LogAccessAsync(
                    currentUserId,
                    "Patient",
                    "Create",
                    $"PatientId: {patient.Id}, MRN: {patient.MedicalRecordNumber}"
                );

                var response = _mapper.Map<PatientResponseDto>(patient);
                
                return CreatedAtAction(
                    nameof(GetPatient),
                    new { id = patient.Id },
                    response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating patient");
                return StatusCode(500, "An error occurred while creating the patient");
            }
        }

        /// <summary>
        /// Update an existing patient
        /// </summary>
        /// <param name="id">Patient ID</param>
        /// <param name="patientDto">Updated patient data</param>
        /// <returns>Updated patient</returns>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin,Physician,Nurse,Receptionist")]
        [ProducesResponseType(typeof(PatientResponseDto), 200)]
        [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<PatientResponseDto>> UpdatePatient(
            Guid id,
            [FromBody] PatientUpdateDto patientDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                // Check if user has permission to update this patient
                if (!await _patientService.CanAccessPatientAsync(id, currentUserId, currentUserRole))
                {
                    return Forbid("You do not have permission to update this patient's information");
                }

                var existingPatient = await _patientService.GetPatientAsync(id);
                if (existingPatient == null)
                {
                    return NotFound($"Patient with ID {id} not found");
                }

                var updatedPatient = await _patientService.UpdatePatientAsync(id, patientDto, currentUserId);

                // Log update
                await _auditService.LogAccessAsync(
                    currentUserId,
                    "Patient",
                    "Update",
                    $"PatientId: {id}"
                );

                var response = _mapper.Map<PatientResponseDto>(updatedPatient);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating patient {PatientId}", id);
                return StatusCode(500, "An error occurred while updating the patient");
            }
        }

        /// <summary>
        /// Get patient's medical history
        /// </summary>
        /// <param name="id">Patient ID</param>
        /// <returns>Patient's medical history</returns>
        [HttpGet("{id:guid}/medical-history")]
        [Authorize(Roles = "Admin,Physician,Nurse")]
        [ProducesResponseType(typeof(List<MedicalHistoryDto>), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<List<MedicalHistoryDto>>> GetPatientMedicalHistory(Guid id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                if (!await _patientService.CanAccessPatientAsync(id, currentUserId, currentUserRole))
                {
                    return Forbid("You do not have permission to access this patient's medical history");
                }

                var history = await _patientService.GetPatientMedicalHistoryAsync(id);
                
                // Log access
                await _auditService.LogAccessAsync(
                    currentUserId,
                    "Patient",
                    "ViewMedicalHistory",
                    $"PatientId: {id}"
                );

                return Ok(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving medical history for patient {PatientId}", id);
                return StatusCode(500, "An error occurred while retrieving medical history");
            }
        }

        /// <summary>
        /// Search patients by various criteria
        /// </summary>
        /// <param name="searchDto">Search criteria</param>
        /// <returns>List of matching patients</returns>
        [HttpPost("search")]
        [Authorize(Roles = "Admin,Physician,Nurse,Receptionist")]
        [ProducesResponseType(typeof(List<PatientSearchResultDto>), 200)]
        [ProducesResponseType(typeof(ValidationProblemDetails), 400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<List<PatientSearchResultDto>>> SearchPatients(
            [FromBody] PatientSearchDto searchDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                var results = await _patientService.SearchPatientsAsync(searchDto, currentUserRole, currentUserId);

                // Log search
                await _auditService.LogAccessAsync(
                    currentUserId,
                    "Patient",
                    "Search",
                    $"Criteria: {System.Text.Json.JsonSerializer.Serialize(searchDto)}"
                );

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching patients");
                return StatusCode(500, "An error occurred while searching patients");
            }
        }

        /// <summary>
        /// Archive a patient (soft delete)
        /// </summary>
        /// <param name="id">Patient ID</param>
        /// <returns>Success result</returns>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin,Physician")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult> ArchivePatient(Guid id)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                if (!await _patientService.CanAccessPatientAsync(id, currentUserId, currentUserRole))
                {
                    return Forbid("You do not have permission to archive this patient");
                }

                var patient = await _patientService.GetPatientAsync(id);
                if (patient == null)
                {
                    return NotFound($"Patient with ID {id} not found");
                }

                await _patientService.ArchivePatientAsync(id, currentUserId);

                // Log archival
                await _auditService.LogAccessAsync(
                    currentUserId,
                    "Patient",
                    "Archive",
                    $"PatientId: {id}"
                );

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error archiving patient {PatientId}", id);
                return StatusCode(500, "An error occurred while archiving the patient");
            }
        }

        /// <summary>
        /// Get patient's vital signs history
        /// </summary>
        /// <param name="id">Patient ID</param>
        /// <param name="days">Number of days to look back (default: 30)</param>
        /// <returns>Vital signs history</returns>
        [HttpGet("{id:guid}/vital-signs")]
        [Authorize(Roles = "Admin,Physician,Nurse,Technician")]
        [ProducesResponseType(typeof(List<VitalSignsDto>), 200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<ActionResult<List<VitalSignsDto>>> GetPatientVitalSigns(
            Guid id,
            [FromQuery] int days = 30)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var currentUserRole = GetCurrentUserRole();

                if (!await _patientService.CanAccessPatientAsync(id, currentUserId, currentUserRole))
                {
                    return Forbid("You do not have permission to access this patient's vital signs");
                }

                var vitalSigns = await _patientService.GetPatientVitalSignsAsync(id, days);

                // Log access
                await _auditService.LogAccessAsync(
                    currentUserId,
                    "Patient",
                    "ViewVitalSigns",
                    $"PatientId: {id}, Days: {days}"
                );

                return Ok(vitalSigns);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving vital signs for patient {PatientId}", id);
                return StatusCode(500, "An error occurred while retrieving vital signs");
            }
        }

        #region Private Helper Methods

        private string GetCurrentUserId()
        {
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
        }

        private string GetCurrentUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        }

        #endregion
    }
} 