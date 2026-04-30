# Anemia Scan API

## Overview
Anemia Scan API is an ASP.NET Core Web API built with .NET 10.0. It provides machine‑learning based analysis of medical images to support early detection of anemia.

## Core Features
- **Image Analysis** – Upload conjunctiva, fingernail, or palm images for automated anemia assessment.
- **Machine Learning Core** – Central ML component located in `Infrastructure/ML` processes images and returns confidence scores.
- **Authorization** – Secure endpoints for user registration, login, token refresh, and sign‑out implemented in `Controllers/AuthorizationController.cs`.
- **Analysis Endpoint** – Image upload and prediction logic in `Controllers/AnalysisController.cs`.
- **Future Extensions** – Planned MongoDB persistence, JWT authentication, and Docker deployment.

## Architecture
- **Controllers** – API surface (`AuthorizationController`, `AnalysisController`).
- **Services** – Business logic and ML orchestration (`Infrastructure/Services`).
- **ML Module** – Core machine‑learning models and preprocessing (`Infrastructure/ML`).
- **Filters** – Request validation and error handling.

## Prerequisites
- .NET 10.0 SDK
- Visual Studio 2022 or VS Code with C# extensions
- (Optional) MongoDB instance for data persistence

## Getting Started
```bash
# Clone the repository
git clone https://github.com/SoWiBe/AnemiaScanApi.git
cd AnemiaScanApi

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the API
dotnet run --project AnemiaScanApi
```
The API will be available at `https://localhost:5001` with Swagger UI enabled in development mode.

## API Documentation
Swagger UI provides interactive documentation for all endpoints at the root URL when the API runs in development.

## Project Structure
```
AnemiaScanApi/
├─ Controllers/
│   ├─ AuthorizationController.cs   # Auth flow (sign‑in, sign‑up, token refresh, sign‑out)
│   └─ AnalysisController.cs       # Image analysis endpoint
├─ Infrastructure/
│   ├─ ML/                         # Core ML models and preprocessing logic
│   └─ Services/                   # Business services
├─ Filters/                        # Validation filters
├─ Models/                         # Request/response DTOs
└─ Program.cs                     # Application startup
```

## Contributing
Contributions are welcome. Please follow these steps:
1. Fork the repository.
2. Create a feature branch (`git checkout -b feature/YourFeature`).
3. Commit your changes with clear messages.
4. Push to your fork and open a pull request.

## License
This project is licensed under the MIT License.

## Contact
- **Maintainer:** Aleks Dantist – aleksey.dantist@gmail.com
- **GitHub:** https://github.com/SoWiBe/AnemiaScanApi

## Disclaimer
The API is intended for **screening purposes only** and does not replace professional medical diagnosis. Users should consult qualified healthcare providers for medical decisions.

---


## Project Overview
Anemia Scan API is a server‑side ASP.NET Core Web API built with .NET 10.0. It provides machine‑learning based analysis of medical images to support early detection of anemia.

## Core Features
- **Image Analysis** – Upload conjunctiva, fingernail, or palm images for automated anemia assessment.
- **Machine Learning Core** – Central ML component located in `Infrastructure/ML` that processes images and returns confidence scores.
- **Authorization** – Secure endpoints for user registration, login, token refresh and sign‑out, implemented in `Controllers/AuthorizationController.cs`.
- **Analysis Endpoint** – Image upload and prediction logic in `Controllers/AnalysisController.cs`.
- **Future Extensions** – Planned MongoDB persistence, JWT authentication, and Docker deployment.

## Architecture
- **Controllers** – Web API surface (`AuthorizationController`, `AnalysisController`).
- **Services** – Business logic and ML orchestration (`Infrastructure/Services`).
- **ML Module** – Core machine‑learning models and preprocessing (`Infrastructure/ML`).
- **Filters** – Request validation and error handling.

## Prerequisites
- .NET 10.0 SDK
- Visual Studio 2022 or VS Code with C# extensions
- (Optional) MongoDB instance for data persistence

## Getting Started
```bash
# Clone repository
git clone https://github.com/SoWiBe/AnemiaScanApi.git
cd AnemiaScanApi

# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Run the API
dotnet run --project AnemiaScanApi
```
The API will be available at `https://localhost:5001` with Swagger UI enabled in development mode.

## API Documentation
Swagger UI provides interactive documentation for all endpoints. Access it at the root URL when the API runs in development.

## Project Structure
```
AnemiaScanApi/
├─ Controllers/
│   ├─ AuthorizationController.cs   # Auth flow (sign‑in, sign‑up, token refresh, sign‑out)
│   └─ AnalysisController.cs       # Image analysis endpoint
├─ Infrastructure/
│   ├─ ML/                         # Core ML models and preprocessing logic
│   └─ Services/                   # Business services
├─ Filters/                        # Validation filters
├─ Models/                         # Request/response DTOs
└─ Program.cs                     # Application startup
```

## Contributing
Contributions are welcome. Please follow these steps:
1. Fork the repository.
2. Create a feature branch (`git checkout -b feature/YourFeature`).
3. Commit your changes with clear messages.
4. Push to your fork and open a pull request.

## License
This project is licensed under the MIT License.

## Contact
- **Maintainer:** Aleks Dantist – aleksey.dantist@gmail.com
- **GitHub:** https://github.com/SoWiBe/AnemiaScanApi

## Disclaimer
The API is intended for **screening purposes only** and does not replace professional medical diagnosis. Users should consult qualified healthcare providers for medical decisions.l diagnosis. Always consult healthcare providers for medical advice.

---

**Made with ❤️ for better healthcare accessibility**
