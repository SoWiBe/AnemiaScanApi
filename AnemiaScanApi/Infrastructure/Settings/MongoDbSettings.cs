namespace AnemiaScanApi.Settings;

/// <summary>
/// Configuration settings for MongoDB.
/// </summary>
public class MongoDbSettings
{
    /// <summary>
    /// Connection string for MongoDB.
    /// </summary>
    public string ConnectionString { get; set; }
    /// <summary>
    /// Name of the MongoDB database.
    /// </summary>
    public string DatabaseName { get; set; }
    /// <summary>
    /// A dictionary of collection names and their corresponding names.
    /// </summary>
    public Dictionary<string, string> Collections { get; set; }
}