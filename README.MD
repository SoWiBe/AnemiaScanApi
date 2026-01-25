# 🩸 SmartAnemiaScan API

ASP.NET Core Web API for automated anemia detection using Machine Learning image analysis.

---

## 🎯 What?

**AnemiaScan API** is a REST API service that analyzes medical images (conjunctiva, fingernails, palms) to detect anemia indicators using machine learning models.

### Key Features
- 📸 **Image Upload & Analysis** - Upload images for ML-based anemia detection
- 🤖 **ML-Powered Predictions** - Returns confidence scores and severity levels
- 👤 **User Management** - Registration and authentication (planned)
- 💾 **MongoDB Storage** - Stores analysis history and images (planned)
- 📱 **Mobile-Ready** - Built for React Native mobile app integration

---

## 💡 Why?

### The Problem
Anemia affects over **1.6 billion people** globally, but early detection often requires expensive lab tests and medical expertise.

### The Solution
This API enables:
- ✅ **Non-invasive screening** through smartphone images
- ✅ **Quick results** - Instant ML analysis in seconds
- ✅ **Accessible healthcare** - No lab visits required
- ✅ **Early detection** - Catch anemia before symptoms worsen

### Use Cases
- Remote health monitoring in underserved areas
- Quick pre-screening before clinical tests
- Personal health tracking
- Research data collection

---

## 🚀 How?

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [MongoDB](https://www.mongodb.com/) (optional, for data persistence)
- IDE: Rider

### Quick Start

1️⃣ **Clone the repository**
```bash
git clone https://github.com/yourusername/AnemiaScanApi.git
cd AnemiaScanApi
```

2️⃣ **Restore dependencies**
```bash
dotnet restore
```

3️⃣ **Run the API**
```bash
dotnet run --project AnemiaScanApi
```

4️⃣ **Open Swagger UI**
```
https://localhost:5001
```

### API Usage

#### Analyze Image for Anemia
```bash
curl -X POST https://localhost:5001/analysis/anemia \
  -H "Content-Type: multipart/form-data" \
  -F "AnemiaImage=@path/to/image.jpg"
```

**Response:**
```json
{
  "analysisId": "64f8a2b3c9e1a2b3c4d5e6f7",
  "confidence": 0.87,
  "prediction": "anemic",
  "severity": "mild",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

### Project Structure
```
AnemiaScanApi/
├── Controllers/          # API endpoints
│   └── AnalysisController.cs
├── Models/              # Request/Response models
│   ├── Requests/
│   └── Responses/
├── Services/            # Business logic (ML, Database)
├── Filters/             # Validation filters
└── Program.cs           # App configuration
```

### Supported Image Types
- **Conjunctiva** (inner eyelid) - Most accurate
- **Fingernails** - Good indicator
- **Palms** - Alternative method

### Image Requirements
- **Formats:** JPG, PNG, JPEG
- **Max Size:** 10 MB
- **Quality:** Clear, well-lit images for best results

---

## 📚 Documentation

- **API Specification:** See `docs/api-specification.md`
- **Architecture Decisions:** See `docs/adr/`
- **Swagger UI:** Available at root path when running in Development mode

---

## 🛠️ Technology Stack

| Component | Technology |
|-----------|-----------|
| Framework | ASP.NET Core 8.0 |
| Language | C# 12 |
| Database | MongoDB (planned) |
| ML Framework | TBD (ML.NET / Python service) |
| API Docs | Swagger/OpenAPI |

---

## 🗺️ Roadmap

- [x] Basic API structure
- [x] Image upload endpoint
- [x] Swagger documentation
- [ ] ML model integration
- [ ] MongoDB persistence
- [ ] User authentication (JWT)
- [ ] Analysis history
- [ ] Image preprocessing service
- [ ] Docker containerization
- [ ] Cloud deployment

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## 👥 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

1. Fork the project
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

## 📧 Contact

**Anemia Scan Team**
- Email: aleksey.dantist@gmail.com
- GitHub: [@SoWiBe](https://github.com/SoWiBe)

---

## ⚠️ Disclaimer

This API is for **screening purposes only** and should not replace professional medical diagnosis. Always consult healthcare providers for medical advice.

---

**Made with ❤️ for better healthcare accessibility**
