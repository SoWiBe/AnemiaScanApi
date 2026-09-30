namespace AnemiaScanApi.Settings;

/// <summary>
/// Configuration settings for MongoDB.
/// Имена коллекций сюда не выносятся — они в
/// <see cref="Common.Constants.MongoCollection"/>.
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
}
