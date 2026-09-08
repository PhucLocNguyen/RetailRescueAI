using Microsoft.AspNetCore.Mvc;
using RetailRescueAI.Backend.DTOs;
using RetailRescueAI.Backend.Services.AI;

namespace RetailRescueAI.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatbotController : ControllerBase
{
    private readonly ManagerChatbotService _chatbotService;

    public ChatbotController(ManagerChatbotService chatbotService)
    {
        _chatbotService = chatbotService;
    }

    [HttpPost("message")]
    public async Task<ActionResult<ChatResponse>> SendMessage([FromBody] ChatRequest request)
    {
        var response = await _chatbotService.ProcessChatAsync(request);
        return Ok(response);
    }
}

