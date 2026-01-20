using AIAgentsBackend.Controllers.Models;
using AIAgentsBackend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AIAgentsBackend.Controllers;

/// <summary>
/// Controller for managing conversation threads.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ThreadsController : ControllerBase
{
    private readonly IThreadRepository _threadRepository;
    private readonly ILogger<ThreadsController> _logger;

    public ThreadsController(
        IThreadRepository threadRepository,
        ILogger<ThreadsController> logger)
    {
        _threadRepository = threadRepository ?? throw new ArgumentNullException(nameof(threadRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets all messages for a specific thread by thread ID.
    /// </summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The thread messages.</returns>
    [HttpGet("{threadId}/messages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ThreadMessagesResponse>> GetThreadMessages(
        string threadId, 
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(threadId))
            return BadRequest("Thread ID is required");

        _logger.LogInformation("Getting thread messages for threadId: {ThreadId}", threadId);

        var messages = await _threadRepository.GetThreadMessagesAsync(threadId, cancellationToken);
        var messagesList = messages.ToList();

        if (!messagesList.Any())
        {
            _logger.LogWarning("No messages found for thread: {ThreadId}", threadId);
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

