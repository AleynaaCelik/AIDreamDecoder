﻿using AIDreamDecoder.Application.Dtos.DreamDtos;
using AIDreamDecoder.Application.Interfaces;
using AIDreamDecoder.Domain.Entities;
using AIDreamDecoder.Domain.Enums;
using AIDreamDecoder.Infrastructure.Persistence.Context;
using AIDreamDecoder.Infrastructure.Repositories; // Bu satırı ekleyin
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static OpenAI.ObjectModels.StaticValues.AssistantsStatics.MessageStatics;

namespace AIDreamDecoder.Infrastructure.Services
{
    public class DreamService : IDreamService
    {
        private readonly IAIDreamInterpreterService _aiService;
        private readonly IDreamRepository _dreamRepository;
        private readonly ILogger<DreamService> _logger;
        private readonly IUserRepository _userRepository;
        private readonly AIDreamDecoderDbContext _dbContext;

        public DreamService(IDreamRepository dreamRepository, IAIDreamInterpreterService aiService, ILogger<DreamService> logger, IUserRepository userRepository, AIDreamDecoderDbContext dbContext)
        {
            _dreamRepository = dreamRepository;
            _aiService = aiService;
            _logger = logger;
            _userRepository = userRepository;
            _dbContext = dbContext;
        }

        public async Task SomeMethod(Guid userId)
        {
            var user = await _userRepository.FindByIdAsync(userId);
            if (user == null)
            {
                user = await _userRepository.CreateAsync(new User
                {
                    Name = "Default Name",
                    Email = "default@example.com"
                });
            }
        }
        public async Task<List<DreamDto>> GetDreamsAsync()
        {
            var dreams = await _dreamRepository.GetAllDreamsAsync();
            return dreams.Select(dream => new DreamDto
            {
                Id = dream.Id,
                Description = dream.Description,
            }).ToList();
        }

        public async Task<DreamDto> GetDreamByIdAsync(Guid id)
        {
            var dream = await _dreamRepository.GetDreamByIdAsync(id);
            if (dream == null) return null;

            return new DreamDto
            {
                Id = dream.Id,
                Description = dream.Description,
            };
        }

        public async Task<User> GetUserByIdAsync(Guid userId)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        }

        public async Task<Guid> AddDreamAsync(DreamDto dreamDto)
        {
            try
            {
                _logger.LogInformation("Starting dream interpretation for user {Id}", dreamDto.UserId);

                // Get AI interpretation
                var interpretation = await _aiService.InterpretDreamAsync(dreamDto.Description);

                var dream = new Dream
                {
                    Id = Guid.NewGuid(), // Yeni bir ID oluştur
                    UserId = dreamDto.UserId, // Kullanıcı ID'sini ekle
                    Description = dreamDto.Description,
                    Analysis = new DreamAnalysis
                    {
                        Id = Guid.NewGuid(), // Analiz için yeni bir ID oluştur
                        AnalysisResult = interpretation,
                        Status = AnalysisStatus.Completed,
                        CreatedAt = DateTime.UtcNow
                    }
                };

                await _dreamRepository.AddAsync(dream);
                _logger.LogInformation("Successfully saved dream and interpretation for user {Id}", dreamDto.UserId);

                return dream.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing dream for user {Id}", dreamDto.UserId);
                throw new ApplicationException("Failed to process dream interpretation", ex);
            }
            /*var dream1 = new Dream
            {
                Id = Guid.NewGuid(),
                Description = dreamDto.Description,
            };

            await _dreamRepository.AddDreamAsync(dream);
            return dream.Id;*/
        }

        public async Task<DreamDto> AddDreamWithInterpretationAsync(DreamDto dreamDto)
        {
            // Get AI interpretation
            var interpretation = await _aiService.InterpretDreamAsync(dreamDto.Description);

            // Create dream entity
            var dream = new Dream
            {
                UserId = dreamDto.Id,
                Description = dreamDto.Description,
                Analysis = new DreamAnalysis
                {
                    AnalysisResult = interpretation,
                    Status = AnalysisStatus.Completed
                }
            };

            // Save to database
            await _dreamRepository.AddAsync(dream);

            // Return DTO
            return dreamDto; //MapToDto burada hata verdiği için böyle döndüm
        }

        public async Task<bool> DeleteDreamAsync(Guid id)
        {
            return await _dreamRepository.DeleteDreamAsync(id);
        }

        public async Task<Guid> AddDreamWithUserTransactionAsync(Guid userId, DreamDto dreamDto)
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                _logger.LogInformation("Transaction started for user {UserId}", userId);

                // Kullanıcıyı bul veya oluştur
                var user = await _userRepository.FindByIdAsync(userId)
                           ?? await _userRepository.CreateAsync(new User { Id = userId });

                _logger.LogInformation("User {UserId} found or created", userId);

                // AI tabanlı rüya analizi al
                var interpretation = await _aiService.InterpretDreamAsync(dreamDto.Description);

                // Yeni rüya oluştur
                var dream = new Dream
                {
                    UserId = user.Id,
                    Description = dreamDto.Description,
                    Analysis = new DreamAnalysis
                    {
                        AnalysisResult = interpretation,
                        Status = AnalysisStatus.Completed,
                        CreatedAt = DateTime.UtcNow
                    }
                };

                // Rüyayı veritabanına ekle
                await _dreamRepository.AddAsync(dream);

                // Transaction'ı onayla
                await transaction.CommitAsync();
                _logger.LogInformation("Transaction committed successfully for user {UserId}", userId);

                return dream.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during transaction for user {UserId}", userId);
                await transaction.RollbackAsync();
                throw new ApplicationException("Failed to process transaction", ex);
            }
        }
    }
}