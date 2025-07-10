using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.DataProtection;
using PatientCareAPI.Controllers;
using PatientCareAPI.Models;
using PatientCareAPI.DTOs;
using PatientCareAPI.Services;
using PatientCareAPI.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace PatientCareAPI.Tests.Controllers
{
    public class PatientsControllerTests : IDisposable
    {
        private readonly Mock<IPatientService> _mockPatientService;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<ILogger<PatientsController>> _mockLogger;
        private readonly Mock<IDataProtectionProvider> _mockDataProtectionProvider;
        private readonly Mock<IDataProtector> _mockDataProtector;
        private readonly Mock<IAuditService> _mockAuditService;
        private readonly Mock<PatientCareDbContext> _mockContext;
        private readonly PatientsController _controller;

        public PatientsControllerTests()
        {
            _mockPatientService = new Mock<IPatientService>();
            _mockMapper = new Mock<IMapper>();
            _mockLogger = new Mock<ILogger<PatientsController>>();
            _mockDataProtectionProvider = new Mock<IDataProtectionProvider>();
            _mockDataProtector = new Mock<IDataProtector>();
            _mockAuditService = new Mock<IAuditService>();
            _mockContext = new Mock<PatientCareDbContext>();

            // Setup data protection
            _mockDataProtectionProvider
                .Setup(x => x.CreateProtector("PatientData"))
                .Returns(_mockDataProtector.Object);

            _controller = new PatientsController(
                _mockContext.Object,
                _mockPatientService.Object,
                _mockMapper.Object,
                _mockLogger.Object,
                _mockDataProtectionProvider.Object,
                _mockAuditService.Object
            );

            // Setup controller context with authenticated user
            SetupControllerContext();
        }

        private void SetupControllerContext()
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "test-user-id"),
                new(ClaimTypes.Role, "Physician"),
                new(ClaimTypes.Email, "test@example.com")
            };

            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var principal = new ClaimsPrincipal(identity);

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            };
        }

        [Fact]
        public async Task GetPatients_WithValidParameters_ReturnsOkResult()
        {
            // Arrange
            var expectedResponse = new PatientListResponseDto
            {
                Patients = new List<PatientResponseDto>
                {
                    new() { Id = Guid.NewGuid(), FirstName = "John", LastName = "Doe" },
                    new() { Id = Guid.NewGuid(), FirstName = "Jane", LastName = "Smith" }
                },
                TotalCount = 2,
                PageSize = 10,
                CurrentPage = 1,
                TotalPages = 1
            };

            _mockPatientService
                .Setup(x => x.GetPatientsAsync(1, 10, null, null, null, "Physician", "test-user-id"))
                .ReturnsAsync(expectedResponse);

            _mockAuditService
                .Setup(x => x.LogAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.GetPatients(1, 10);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<PatientListResponseDto>(okResult.Value);
            Assert.Equal(2, returnValue.Patients.Count);
            Assert.Equal(2, returnValue.TotalCount);

            // Verify audit logging
            _mockAuditService.Verify(x => x.LogAccessAsync(
                "test-user-id",
                "Patient",
                "List",
                It.IsAny<string>()
            ), Times.Once);
        }

        [Fact]
        public async Task GetPatients_WithInvalidPageSize_UsesDefaultPageSize()
        {
            // Arrange
            var expectedResponse = new PatientListResponseDto
            {
                Patients = new List<PatientResponseDto>(),
                TotalCount = 0,
                PageSize = 10,
                CurrentPage = 1,
                TotalPages = 0
            };

            _mockPatientService
                .Setup(x => x.GetPatientsAsync(1, 10, null, null, null, "Physician", "test-user-id"))
                .ReturnsAsync(expectedResponse);

            _mockAuditService
                .Setup(x => x.LogAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act - passing invalid page size (200 > 100 max)
            var result = await _controller.GetPatients(1, 200);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            
            // Verify that the service was called with corrected page size (10)
            _mockPatientService.Verify(x => x.GetPatientsAsync(
                1, 10, null, null, null, "Physician", "test-user-id"
            ), Times.Once);
        }

        [Fact]
        public async Task GetPatient_WithValidId_ReturnsPatientDetails()
        {
            // Arrange
            var patientId = Guid.NewGuid();
            var expectedResponse = new PatientDetailResponseDto
            {
                Id = patientId,
                FirstName = "John",
                LastName = "Doe",
                MedicalRecordNumber = "MRN123456",
                DateOfBirth = new DateTime(1990, 1, 1),
                Gender = Gender.Male
            };

            _mockPatientService
                .Setup(x => x.CanAccessPatientAsync(patientId, "test-user-id", "Physician"))
                .ReturnsAsync(true);

            _mockPatientService
                .Setup(x => x.GetPatientDetailAsync(patientId))
                .ReturnsAsync(expectedResponse);

            _mockAuditService
                .Setup(x => x.LogAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.GetPatient(patientId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<PatientDetailResponseDto>(okResult.Value);
            Assert.Equal(patientId, returnValue.Id);
            Assert.Equal("John", returnValue.FirstName);
            Assert.Equal("Doe", returnValue.LastName);

            // Verify audit logging
            _mockAuditService.Verify(x => x.LogAccessAsync(
                "test-user-id",
                "Patient",
                "View",
                $"PatientId: {patientId}"
            ), Times.Once);
        }

        [Fact]
        public async Task GetPatient_WithUnauthorizedAccess_ReturnsForbid()
        {
            // Arrange
            var patientId = Guid.NewGuid();

            _mockPatientService
                .Setup(x => x.CanAccessPatientAsync(patientId, "test-user-id", "Physician"))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.GetPatient(patientId);

            // Assert
            var forbidResult = Assert.IsType<ForbidResult>(result.Result);
            Assert.Contains("You do not have permission", forbidResult.AuthenticationSchemes.FirstOrDefault() ?? "");
        }

        [Fact]
        public async Task GetPatient_WithNonExistentId_ReturnsNotFound()
        {
            // Arrange
            var patientId = Guid.NewGuid();

            _mockPatientService
                .Setup(x => x.CanAccessPatientAsync(patientId, "test-user-id", "Physician"))
                .ReturnsAsync(true);

            _mockPatientService
                .Setup(x => x.GetPatientDetailAsync(patientId))
                .ReturnsAsync((PatientDetailResponseDto?)null);

            // Act
            var result = await _controller.GetPatient(patientId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.Contains($"Patient with ID {patientId} not found", notFoundResult.Value?.ToString());
        }

        [Fact]
        public async Task CreatePatient_WithValidData_ReturnsCreatedResult()
        {
            // Arrange
            var patientCreateDto = new PatientCreateDto
            {
                FirstName = "John",
                LastName = "Doe",
                DateOfBirth = new DateTime(1990, 1, 1),
                Gender = Gender.Male,
                Email = "john.doe@example.com",
                Phone = "123-456-7890",
                Address = new AddressDto
                {
                    Street = "123 Main St",
                    City = "Anytown",
                    State = "CA",
                    ZipCode = "12345",
                    Country = "USA"
                },
                EmergencyContact = new EmergencyContactDto
                {
                    Name = "Jane Doe",
                    Relationship = "Spouse",
                    Phone = "123-456-7891"
                },
                Insurance = new InsuranceDto
                {
                    Provider = "Health Plus",
                    PolicyNumber = "POL123456",
                    CoverageType = CoverageType.Primary,
                    EffectiveDate = DateTime.Now.AddYears(-1)
                }
            };

            var createdPatient = new Patient
            {
                Id = Guid.NewGuid(),
                FirstName = "John",
                LastName = "Doe",
                MedicalRecordNumber = "MRN123456",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var patientResponseDto = new PatientResponseDto
            {
                Id = createdPatient.Id,
                FirstName = "John",
                LastName = "Doe",
                MedicalRecordNumber = "MRN123456"
            };

            var validationResult = new ValidationResult { IsValid = true };

            _mockPatientService
                .Setup(x => x.ValidatePatientCreateAsync(patientCreateDto))
                .ReturnsAsync(validationResult);

            _mockPatientService
                .Setup(x => x.CreatePatientAsync(patientCreateDto, "test-user-id"))
                .ReturnsAsync(createdPatient);

            _mockMapper
                .Setup(x => x.Map<PatientResponseDto>(createdPatient))
                .Returns(patientResponseDto);

            _mockAuditService
                .Setup(x => x.LogAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.CreatePatient(patientCreateDto);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            var returnValue = Assert.IsType<PatientResponseDto>(createdResult.Value);
            Assert.Equal(createdPatient.Id, returnValue.Id);
            Assert.Equal("John", returnValue.FirstName);
            Assert.Equal("Doe", returnValue.LastName);

            // Verify audit logging
            _mockAuditService.Verify(x => x.LogAccessAsync(
                "test-user-id",
                "Patient",
                "Create",
                It.IsAny<string>()
            ), Times.Once);
        }

        [Fact]
        public async Task CreatePatient_WithInvalidData_ReturnsBadRequest()
        {
            // Arrange
            var patientCreateDto = new PatientCreateDto
            {
                FirstName = "John",
                LastName = "Doe"
                // Missing required fields
            };

            var validationResult = new ValidationResult
            {
                IsValid = false,
                Errors = new List<string> { "Date of birth is required", "Gender is required" }
            };

            _mockPatientService
                .Setup(x => x.ValidatePatientCreateAsync(patientCreateDto))
                .ReturnsAsync(validationResult);

            // Act
            var result = await _controller.CreatePatient(patientCreateDto);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.NotNull(badRequestResult.Value);
        }

        [Fact]
        public async Task UpdatePatient_WithValidData_ReturnsOkResult()
        {
            // Arrange
            var patientId = Guid.NewGuid();
            var patientUpdateDto = new PatientUpdateDto
            {
                Id = patientId,
                FirstName = "John",
                LastName = "Smith", // Changed last name
                Email = "john.smith@example.com"
            };

            var existingPatient = new Patient
            {
                Id = patientId,
                FirstName = "John",
                LastName = "Doe",
                MedicalRecordNumber = "MRN123456"
            };

            var updatedPatient = new Patient
            {
                Id = patientId,
                FirstName = "John",
                LastName = "Smith",
                MedicalRecordNumber = "MRN123456",
                UpdatedAt = DateTime.UtcNow
            };

            var patientResponseDto = new PatientResponseDto
            {
                Id = patientId,
                FirstName = "John",
                LastName = "Smith",
                MedicalRecordNumber = "MRN123456"
            };

            _mockPatientService
                .Setup(x => x.CanAccessPatientAsync(patientId, "test-user-id", "Physician"))
                .ReturnsAsync(true);

            _mockPatientService
                .Setup(x => x.GetPatientAsync(patientId))
                .ReturnsAsync(existingPatient);

            _mockPatientService
                .Setup(x => x.UpdatePatientAsync(patientId, patientUpdateDto, "test-user-id"))
                .ReturnsAsync(updatedPatient);

            _mockMapper
                .Setup(x => x.Map<PatientResponseDto>(updatedPatient))
                .Returns(patientResponseDto);

            _mockAuditService
                .Setup(x => x.LogAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.UpdatePatient(patientId, patientUpdateDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<PatientResponseDto>(okResult.Value);
            Assert.Equal("Smith", returnValue.LastName);

            // Verify audit logging
            _mockAuditService.Verify(x => x.LogAccessAsync(
                "test-user-id",
                "Patient",
                "Update",
                $"PatientId: {patientId}"
            ), Times.Once);
        }

        [Fact]
        public async Task GetPatientMedicalHistory_WithValidId_ReturnsHistory()
        {
            // Arrange
            var patientId = Guid.NewGuid();
            var expectedHistory = new List<MedicalHistoryDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Condition = "Hypertension",
                    DiagnosisDate = DateTime.Now.AddYears(-2),
                    Status = ConditionStatus.Active,
                    TreatingPhysician = "Dr. Smith"
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    Condition = "Diabetes Type 2",
                    DiagnosisDate = DateTime.Now.AddYears(-1),
                    Status = ConditionStatus.UnderTreatment,
                    TreatingPhysician = "Dr. Johnson"
                }
            };

            _mockPatientService
                .Setup(x => x.CanAccessPatientAsync(patientId, "test-user-id", "Physician"))
                .ReturnsAsync(true);

            _mockPatientService
                .Setup(x => x.GetPatientMedicalHistoryAsync(patientId))
                .ReturnsAsync(expectedHistory);

            _mockAuditService
                .Setup(x => x.LogAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.GetPatientMedicalHistory(patientId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<List<MedicalHistoryDto>>(okResult.Value);
            Assert.Equal(2, returnValue.Count);
            Assert.Equal("Hypertension", returnValue[0].Condition);
            Assert.Equal("Diabetes Type 2", returnValue[1].Condition);

            // Verify audit logging
            _mockAuditService.Verify(x => x.LogAccessAsync(
                "test-user-id",
                "Patient",
                "ViewMedicalHistory",
                $"PatientId: {patientId}"
            ), Times.Once);
        }

        [Fact]
        public async Task ArchivePatient_WithValidId_ReturnsNoContent()
        {
            // Arrange
            var patientId = Guid.NewGuid();
            var existingPatient = new Patient
            {
                Id = patientId,
                FirstName = "John",
                LastName = "Doe",
                Status = PatientStatus.Active
            };

            _mockPatientService
                .Setup(x => x.CanAccessPatientAsync(patientId, "test-user-id", "Physician"))
                .ReturnsAsync(true);

            _mockPatientService
                .Setup(x => x.GetPatientAsync(patientId))
                .ReturnsAsync(existingPatient);

            _mockPatientService
                .Setup(x => x.ArchivePatientAsync(patientId, "test-user-id"))
                .Returns(Task.CompletedTask);

            _mockAuditService
                .Setup(x => x.LogAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.ArchivePatient(patientId);

            // Assert
            Assert.IsType<NoContentResult>(result);

            // Verify archive operation was called
            _mockPatientService.Verify(x => x.ArchivePatientAsync(patientId, "test-user-id"), Times.Once);

            // Verify audit logging
            _mockAuditService.Verify(x => x.LogAccessAsync(
                "test-user-id",
                "Patient",
                "Archive",
                $"PatientId: {patientId}"
            ), Times.Once);
        }

        [Fact]
        public async Task GetPatientVitalSigns_WithValidId_ReturnsVitalSigns()
        {
            // Arrange
            var patientId = Guid.NewGuid();
            var expectedVitalSigns = new List<VitalSignsDto>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    PatientId = patientId,
                    RecordedAt = DateTime.Now.AddDays(-1),
                    Temperature = 98.6,
                    BloodPressure = new BloodPressureDto { Systolic = 120, Diastolic = 80 },
                    HeartRate = 72,
                    RespiratoryRate = 16,
                    OxygenSaturation = 98
                }
            };

            _mockPatientService
                .Setup(x => x.CanAccessPatientAsync(patientId, "test-user-id", "Physician"))
                .ReturnsAsync(true);

            _mockPatientService
                .Setup(x => x.GetPatientVitalSignsAsync(patientId, 30))
                .ReturnsAsync(expectedVitalSigns);

            _mockAuditService
                .Setup(x => x.LogAccessAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.GetPatientVitalSigns(patientId, 30);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnValue = Assert.IsType<List<VitalSignsDto>>(okResult.Value);
            Assert.Single(returnValue);
            Assert.Equal(98.6, returnValue[0].Temperature);
            Assert.Equal(120, returnValue[0].BloodPressure.Systolic);
            Assert.Equal(80, returnValue[0].BloodPressure.Diastolic);

            // Verify audit logging
            _mockAuditService.Verify(x => x.LogAccessAsync(
                "test-user-id",
                "Patient",
                "ViewVitalSigns",
                $"PatientId: {patientId}, Days: 30"
            ), Times.Once);
        }

        public void Dispose()
        {
            // Clean up resources if needed
        }
    }

    // Helper classes for testing
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }
} 