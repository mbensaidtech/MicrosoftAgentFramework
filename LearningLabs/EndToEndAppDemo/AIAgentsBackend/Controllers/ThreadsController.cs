using AIAgentsBackend.Controllers.Models;
using AIAgentsBackend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentsBackend.Controllers;

/// <summary>
/// API for retrieving conversation threads.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ThreadsController : ControllerBase
{
    private readonly IThreadRepository threadRepository;
    private readonly ILogger<ThreadsController> logger;

    public ThreadsController(
        IThreadRepository threadRepository,
        ILogger<ThreadsController> logger)
    {
        this.threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all messages from a conversation thread.
    /// </summary>
    [HttpGet("{threadId}/messages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ThreadMessagesResponse>> GetThreadMessages(
        string threadId, 
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            return BadRequest("Thread ID is required");

        logger.LogInformation("Getting thread messages for threadId: {ThreadId}", threadId);

        var messages = await threadRepository.GetThreadMessagesAsync(threadId, cancellationToken);
        var messagesList = messages.ToList();

        if (!messagesList.Any())
        {
            logger.LogWarning("No messages found for thread: {ThreadId}", threadId);
            return NotFound($"No messages found for thread with ID '{threadId}'");
        }

        var response = new ThreadMessagesResponse
        {
            ThreadId = threadId,
            MessageCount = messagesList.Count,
            Messages = messagesList.Select(m => new MessageDto
            {
                Key = m.Key,
                Timestamp = m.Timestamp,
                MessageText = m.MessageText,
                SerializedMessage = m.SerializedMessage
            }).ToList()
        };

        return Ok(response);
    }
}
