# AnemiaScan API Specification
> Version 1.0 (25.01.2026)

## Overview
REST API для анализа анемии через изображения конъюнктивы, ногтей, ладоней.

**Base URL:** `https://api.anemiascan.com/` (needs to bought domain) <br/>
**Protocol:** HTTPS
**Format:** JSON (кроме multipart/form-data для загрузки изображений)

---

## 1. Analyze Anemia

### Endpoint

> http POST https://api.anemiascan.com/analysis/anemia

### Description
Анализирует загруженное изображение на признаки анемии с использованием ML модели.

### Request

**Headers:**
- `Content-Type: multipart/form-data`

**Body (Form Data):**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| AnemiaImage | File | Yes | Изображение для анализа (JPG, PNG, JPEG). Max 10MB |
| userId | String | Yes | Идентификатор пользователя |
| imageType | String | Yes | Тип изображения (conjunctiva) |

**Example Request:**
```http
POST https://api.anemiascan.com/analysis/anemia
Content-Type: multipart/form-data

AnemiaImage: [binary file data]
userId: [user ID]
imageType: [image type]
```

### Response

**Success Response (200 OK):**

```json
{
    "analysisId": "64f8a2b3c9e1a2b3c4d5e6f7",
    "confidence": 0.87,
    "prediction": "anemic",
    "severity": "mild",
    "timestamp": "2024-01-15T10:30:00Z",
    "imageUrl": "https://storage.anemiascan.com/analyses/64f8a2b3c9e1a2b3c4d5e6f7.jpg"
}
````

**Error Responses:**

- **400 Bad Request - Invalid image:** { "error": "InvalidImage", "message": "File format not supported. Use JPG, PNG, or JPEG", "code": "ERR_INVALID_FORMAT" }
- **400 Bad Request - File too large:** { "error": "FileTooLarge", "message": "File size exceeds 10MB limit", "code": "ERR_FILE_SIZE" }
- **500 Internal Server Error - ML model failure:** { "error": "AnalysisError", "message": "Failed to process image", "code": "ERR_ML_PROCESSING" }

### Database Operations

**MongoDB Collections Used:**

1. **`analyses` collection - Сохранение результата:**

```javascript
{
  _id: ObjectId("64f8a2b3c9e1a2b3c4d5e6f7"),
  userId: "user123",
  imageType: "conjunctiva",
  prediction: "anemic",
  confidence: 0.87,
  severity: "mild",
  imageGridFsId: ObjectId("64f8a2b3c9e1a2b3c4d5e6f8"),
  createdAt: ISODate("2024-01-15T10:30:00Z"),
  modelVersion: "v1.2.0"
}
```

2. **GridFS - Сохранение изображения:**
```javascript
{
  _id: ObjectId("64f8a2b3c9e1a2b3c4d5e6f8"),
  filename: "64f8a2b3c9e1a2b3c4d5e6f7.jpg",
  contentType: "image/jpeg",
  length: 2048576,
  uploadDate: ISODate("2024-01-15T10:30:00Z"),
  metadata: { analysisId: ObjectId("64f8a2b3c9e1a2b3c4d5e6f7"), userId: "user123" }
}
```
3. **`users` collection - Сохранение данных пользователя:**

```javascript
{
  _id: ObjectId("user123"),
  email: "user@example.com",
  createdAt: ISODate("2024-01-15T10:30:00Z")
}
```