﻿using ChatApp.Application.CQRS.ChatMessage.Commands.Models;
using ChatApp.Application.CQRS.ChatMessage.Queries.Response;
using ChatApp.Application.CQRS.Requests.Chat.Models;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ChatApp.Api.Hubs
{
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public class ChatHub : Hub
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ChatHub> _logger;
        public ChatHub(IMediator mediator, ILogger<ChatHub> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }
        public Task SendMessage(SendMessageCommand command)
        {
            var userName = Context.UserIdentifier;
            var broadcastMessage = new ChatMessageResponse
            {
                Sender = userName,
                Message = command.Message,
                Timestamp = DateTime.UtcNow,
                MessageId = Guid.NewGuid()
            };
            _ = Clients.Group(command.ChatRoomId.ToString())
                .SendAsync("ReceiveMessage", broadcastMessage);
            _ = ProcessSendMessageAsync(command);
            return Task.CompletedTask;
        }
 
        public Task EditMessage(EditMessageCommand command)
        {
            var broadcastMessage = new ChatMessageResponse
            {
                Sender = "User",
                Message = command.NewContent,
                Timestamp = DateTime.UtcNow,
                MessageId = command.MessageId
            };
            _ = Clients.Group(command.ChatRoomId.ToString())
                .SendAsync("MessageEdited", broadcastMessage);
            _ = ProcessEditMessageAsync(command);
            return Task.CompletedTask;
        }



        public Task DeleteMessage(DeleteMessageCommand command)
        {
            _ = Clients.Group(command.ChatRoomId.ToString())
                .SendAsync("MessageDeleted", command.MessageId);
            _ = ProcessDeleteMessageAsync(command);
            return Task.CompletedTask;
        }


        public async Task JoinRoom(string chatRoomId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, chatRoomId);
                await Clients.Group(chatRoomId).SendAsync("UserJoined", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining room for ChatRoomId: {ChatRoomId}", chatRoomId);
            }
        }

        public async Task LeaveRoom(string chatRoomId)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
            try
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, chatRoomId);
                await Clients.Group(chatRoomId).SendAsync("UserLeft", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error joining room for ChatRoomId: {ChatRoomId}", chatRoomId);
            }
        }





        #region Private helper
        private async Task ProcessSendMessageAsync(SendMessageCommand command)
        {
            try { await _mediator.Send(command); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SendMessage for ChatRoomId: {ChatRoomId}", command.ChatRoomId);
            }
        }
        private async Task ProcessEditMessageAsync(EditMessageCommand command)
        {
            try { await _mediator.Send(command); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SendMessage for ChatRoomId: {ChatRoomId}", command.ChatRoomId);
            }
        }
        private async Task ProcessDeleteMessageAsync(DeleteMessageCommand command)
        {
            try
            {
                var response = await _mediator.Send(command);
                if (response.Success)
                    await Clients.Group(command.ChatRoomId.ToString())
                        .SendAsync("MessageDeleted", command.MessageId);
                else
                    _logger.LogWarning("DeleteMessage failed for ChatRoomId: {ChatRoomId}. Message: {Message}", command.ChatRoomId, response.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting message in ChatRoomId: {ChatRoomId}", command.ChatRoomId);
            }
        }
        #endregion
    }
}
