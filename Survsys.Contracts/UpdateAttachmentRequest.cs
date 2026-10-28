namespace Survsys.Contracts;

/// <summary>
/// The correction to a stored attachment: what <c>PUT /api/attachments/{id}</c>
/// takes.
/// </summary>
/// <remarks>
/// An attachment's bytes, filename, content type and size are the stored
/// file's own and do not change; the one thing about it that is a filing
/// decision rather than a fact of the file is the correspondence category it is
/// filed under, so that is the one thing this corrects. The category is one of
/// the fixed vocabulary the upload knows, and the endpoint refuses any other.
/// </remarks>
/// <param name="Category">
/// The correspondence category to file the attachment under, one of the codes
/// the upload offers.
/// </param>
public record UpdateAttachmentRequest(string? Category = null);
