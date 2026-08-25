using System.ComponentModel.DataAnnotations;

namespace AnemiaScanApi.Common.Requests.Courses;

/// <summary>
/// Payload to attach a fresh scan to a rescan-checkpoint day.
/// </summary>
public class AttachCheckpointScanRequest
{
    /// <summary>
    /// ID of the freshly created <see cref="AnemiaScan"/>.
    /// </summary>
    [Required] public Guid AnemiaScanId { get; set; }
}
