# Healthcare Patient Care Dashboard

A modern, secure healthcare application built with Angular and .NET Core, designed to demonstrate enterprise-level software engineering practices for healthcare IT environments.

## 🏥 Healthcare Focus

This application addresses real healthcare challenges:
- **Patient Data Management**: Secure handling of sensitive medical information
- **Clinical Workflow Integration**: Streamlined interfaces for healthcare providers
- **Compliance-Ready**: HIPAA-aware security implementations
- **Population Health**: Dashboard views for patient outcome tracking

## 🛠️ Technical Stack

### Frontend
- **Angular 17+** with TypeScript
- **Angular Material** for healthcare-appropriate UI
- **RxJS** for reactive programming patterns
- **JWT Authentication** for secure access

### Backend
- **.NET 8 Web API** with Entity Framework Core
- **Clean Architecture** following enterprise patterns
- **SQL Server** for data persistence
- **JWT Bearer Authentication**
- **Swagger/OpenAPI** for API documentation

### Security & Compliance
- **HIPAA-compliant** data handling patterns
- **Role-based access control** (RBAC)
- **Data encryption** at rest and in transit
- **Audit logging** for compliance tracking

## 🚀 Quick Start

### Prerequisites
- Node.js 18+ and npm
- .NET 8 SDK
- SQL Server or SQL Server Express

### Setup Instructions

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd upmcase
   ```

2. **Backend Setup**
   ```bash
   cd backend
   dotnet restore
   dotnet build
   dotnet ef database update
   dotnet run
   ```

3. **Frontend Setup**
   ```bash
   cd frontend
   npm install
   ng serve
   ```

4. **Access the application**
   - Frontend: http://localhost:4200
   - Backend API: http://localhost:5000
   - Swagger UI: http://localhost:5000/swagger

## 📋 Features Implemented

### Patient Management
- Secure patient data viewing and editing
- Medical history tracking
- Appointment scheduling integration

### Clinical Dashboard
- Real-time patient status monitoring
- Clinical workflow notifications
- Population health metrics

### Security Features
- JWT-based authentication
- Role-based access control
- Data encryption and audit trails
- HIPAA compliance patterns

## 🧪 Testing Strategy

- **Unit Tests**: Frontend (Jasmine/Karma) and Backend (xUnit)
- **Integration Tests**: API endpoint testing
- **Security Tests**: Authentication and authorization validation
- **E2E Tests**: Critical healthcare workflows

## 📊 DevOps & CI/CD

- **Git workflow** with feature branches
- **Automated testing** on commit
- **Docker containers** for consistent deployment
- **Environment configurations** for dev/staging/prod

## 🏗️ Architecture Decisions

### Clean Architecture
- Separation of concerns across layers
- Dependency injection throughout
- Repository pattern for data access
- CQRS pattern for complex operations

### Healthcare-Specific Patterns
- Patient data access logging
- Medical record versioning
- Clinical decision support integration points
- Interoperability standards (HL7 FHIR ready)

## 📝 Technical Documentation

See `/docs` folder for:
- API documentation
- Database schema
- Security implementation guide
- Deployment procedures

## 📞 Contact

Built to showcase healthcare IT engineering capabilities for enterprise environments. 
