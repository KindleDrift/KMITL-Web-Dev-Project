using System.Text;
using Microsoft.EntityFrameworkCore;
using WebDevProject.Data;
using WebDevProject.Models;

namespace WebDevProject.Services
{
    public class BoardService
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public BoardService(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        public object GetBoardSearchDtos(List<Board> boards)
        {
            return boards.Select(b =>
            {
                var visibleParticipants = b.Participants.Where(p => p.UserId != b.AuthorId).ToList();
                var participantCount = visibleParticipants.Count + b.ExternalParticipants.Count;
                var spotsLeft = Math.Max(b.MaxParticipants - participantCount, 0);
                var isOpenPastDeadline = b.CurrentStatus == BoardStatus.Open && b.Deadline <= DateTimeOffset.UtcNow.UtcDateTime;

                var statusClass = b.CurrentStatus switch
                {
                    BoardStatus.Open => isOpenPastDeadline ? "status-closed" : "status-open",
                    BoardStatus.Full => "status-full",
                    BoardStatus.Closed => "status-closed",
                    BoardStatus.Cancelled => "status-cancelled",
                    BoardStatus.Archived => "status-archived",
                    _ => "status-open"
                };

                return new
                {
                    id = b.Id,
                    title = b.Title,
                    description = b.Description,
                    imageUrl = string.IsNullOrWhiteSpace(b.ImageUrl) ? "/images/default-board.png" : b.ImageUrl,
                    status = b.CurrentStatus.ToString(),
                    displayStatus = isOpenPastDeadline ? "Open (Deadline Passed)" : b.CurrentStatus.ToString(),
                    statusClass = statusClass,
                    eventDate = b.EventDate.ToString("dd MMM yyyy"),
                    eventTime = b.EventDate.ToString("HH:mm"),
                    eventDateUtc = b.EventDate.ToString("o"),
                    deadline = b.Deadline.ToString("dd MMM yyyy, HH:mm"),
                    deadlineUtc = b.Deadline.ToString("o"),
                    location = b.Location,
                    tags = b.Tags.Select(t => t.Name).ToList(),
                    joinPolicy = b.JoinPolicy.ToString(),
                    joinPolicyDisplay = b.JoinPolicy == BoardJoinPolicy.FirstComeFirstServe ? "First Come First Serve" : "Application",
                    currentParticipants = participantCount,
                    maxParticipants = b.MaxParticipants,
                    spotsLeft = spotsLeft,
                    author = new
                    {
                        displayName = b.Author?.DisplayName ?? "Unknown",
                        profilePictureUrl = b.Author?.ProfilePictureUrl ?? "/images/default-profile.png"
                    },
                    previewParticipants = visibleParticipants.Take(5).Select(p => new
                    {
                        displayName = p.User?.DisplayName ?? "Participant",
                        profilePictureUrl = p.User?.ProfilePictureUrl ?? "/images/default-profile.png"
                    }).ToList(),
                    totalVisibleParticipants = visibleParticipants.Count
                };
            }).ToList();
        }

        public async Task<(bool Success, string? ImageUrl, string? ErrorMessage)> SaveBoardImageAsync(IFormFile? boardImage, string userId, string? existingImageUrl)
        {
            if (boardImage == null || boardImage.Length <= 0)
            {
                return (true, existingImageUrl, null);
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(boardImage.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(fileExtension))
            {
                return (false, existingImageUrl, "Only image files are allowed (.jpg, .jpeg, .png, .gif, .webp).");
            }

            if (boardImage.Length > 5 * 1024 * 1024)
            {
                return (false, existingImageUrl, "Image file size must not exceed 5MB.");
            }

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "boards");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            if (!string.IsNullOrWhiteSpace(existingImageUrl))
            {
                var oldRelativePath = existingImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
                var oldImagePath = Path.Combine(_environment.WebRootPath, oldRelativePath);
                if (System.IO.File.Exists(oldImagePath))
                {
                    try
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                    catch (Exception)
                    {
                        // Ignore file deletion errors to prevent crashing the upload process
                    }
                }
            }

            var uniqueFileName = $"{userId}_{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await boardImage.CopyToAsync(fileStream);
            }

            return (true, $"/uploads/boards/{uniqueFileName}", null);
        }

        public async Task<(bool Success, string? ErrorMessage)> ApplyTagsToBoardAsync(Board board, List<string>? tags)
        {
            board.Tags.Clear();

            if (tags == null || !tags.Any())
            {
                return (true, null);
            }

            var validatedTags = new List<string>();

            foreach (var tag in tags)
            {
                var trimmedTag = tag?.Trim();
                if (string.IsNullOrWhiteSpace(trimmedTag))
                {
                    continue;
                }

                if (trimmedTag.Length > 50)
                {
                    return (false, $"Tag '{trimmedTag.Substring(0, Math.Min(50, trimmedTag.Length))}...' is too long. Tags must be 50 characters or less.");
                }

                if (!IsValidTag(trimmedTag))
                {
                    return (false, $"Invalid tag '{trimmedTag}'. Tags must contain only letters and single hyphens (not at start or end).");
                }

                var formattedTag = FormatTag(trimmedTag);
                if (!validatedTags.Contains(formattedTag, StringComparer.OrdinalIgnoreCase))
                {
                    validatedTags.Add(formattedTag);
                }
            }

            if (!validatedTags.Any())
            {
                return (true, null);
            }

            var existingTags = await _context.Tags
                .Where(t => validatedTags.Contains(t.Name))
                .ToListAsync();

            var existingNames = new HashSet<string>(existingTags.Select(t => t.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var name in validatedTags)
            {
                if (!existingNames.Contains(name))
                {
                    var newTag = new Tag { Name = name };
                    _context.Tags.Add(newTag);
                    existingTags.Add(newTag);
                }
            }

            await _context.SaveChangesAsync();

            foreach (var tag in existingTags)
            {
                board.Tags.Add(tag);
            }

            return (true, null);
        }

        public GroupManagement ParseGroupManagementOption(string? option)
        {
            return option switch
            {
                "allowOverbooking" => GroupManagement.AllowOverbooking,
                "keepOpenWhenFull" => GroupManagement.KeepOpenWhenFull,
                "increaseMax" => GroupManagement.AllowOverbooking,
                "manualIncrease" => GroupManagement.KeepOpenWhenFull,
                _ => GroupManagement.CloseOnFull
            };
        }

        public string ToGroupManagementOptionValue(GroupManagement option)
        {
            return option switch
            {
                GroupManagement.AllowOverbooking => "allowOverbooking",
                GroupManagement.KeepOpenWhenFull => "keepOpenWhenFull",
                _ => "closeOnFull"
            };
        }

        public BoardJoinPolicy ParseJoinPolicyOption(string? option)
        {
            return option switch
            {
                "fcfs" => BoardJoinPolicy.FirstComeFirstServe,
                _ => BoardJoinPolicy.Application
            };
        }

        public string ToJoinPolicyOptionValue(BoardJoinPolicy option)
        {
            return option switch
            {
                BoardJoinPolicy.FirstComeFirstServe => "fcfs",
                _ => "application"
            };
        }

        public int GetOccupiedSeatCount(Board board)
        {
            return board.Participants.Count(p => p.UserId != board.AuthorId) + board.ExternalParticipants.Count;
        }

        public void UpdateBoardStatusByCapacity(Board board, int occupiedSeats)
        {
            if (board.CurrentStatus is BoardStatus.Closed or BoardStatus.Cancelled or BoardStatus.Archived)
            {
                return;
            }

            if (occupiedSeats >= board.MaxParticipants)
            {
                if (board.GroupManagementOption == GroupManagement.CloseOnFull)
                {
                    board.CurrentStatus = BoardStatus.Full;
                }

                return;
            }

            if (board.CurrentStatus == BoardStatus.Full)
            {
                board.CurrentStatus = BoardStatus.Open;
            }
        }

        public List<string> AutoApproveApplicantsOnJoinPolicyChange(Board board, int boardId, BoardJoinPolicy oldJoinPolicy)
        {
            var approvedApplicantIds = new List<string>();

            if (oldJoinPolicy != BoardJoinPolicy.Application || board.JoinPolicy != BoardJoinPolicy.FirstComeFirstServe)
            {
                return approvedApplicantIds;
            }

            var occupiedSeats = GetOccupiedSeatCount(board);
            var applicantsToApprove = board.Applicants.ToList();

            foreach (var applicant in applicantsToApprove)
            {
                if (occupiedSeats >= board.MaxParticipants && board.GroupManagementOption != GroupManagement.AllowOverbooking)
                {
                    continue;
                }

                _context.BoardApplicants.Remove(applicant);

                var participant = new BoardParticipant
                {
                    BoardId = boardId,
                    UserId = applicant.UserId,
                    JoinedAt = DateTimeOffset.UtcNow.UtcDateTime
                };

                _context.BoardParticipants.Add(participant);
                approvedApplicantIds.Add(applicant.UserId);
                occupiedSeats++;
            }

            UpdateBoardStatusByCapacity(board, occupiedSeats);
            return approvedApplicantIds;
        }

        public void RecalculateStatusAfterBoardSettingChanges(Board board, GroupManagement oldGroupManagement, int oldMaxParticipants)
        {
            if (oldGroupManagement == GroupManagement.CloseOnFull &&
                board.GroupManagementOption != GroupManagement.CloseOnFull &&
                board.CurrentStatus == BoardStatus.Full)
            {
                board.CurrentStatus = BoardStatus.Open;
            }

            if (oldGroupManagement != GroupManagement.CloseOnFull &&
                board.GroupManagementOption == GroupManagement.CloseOnFull)
            {
                var currentOccupied = GetOccupiedSeatCount(board);
                if (currentOccupied >= board.MaxParticipants && board.CurrentStatus == BoardStatus.Open)
                {
                    board.CurrentStatus = BoardStatus.Full;
                }
            }

            if (oldMaxParticipants != board.MaxParticipants)
            {
                var currentOccupied = GetOccupiedSeatCount(board);

                if (board.MaxParticipants > oldMaxParticipants && board.CurrentStatus == BoardStatus.Full)
                {
                    if (currentOccupied < board.MaxParticipants)
                    {
                        board.CurrentStatus = BoardStatus.Open;
                    }
                }
                else if (board.MaxParticipants < oldMaxParticipants && board.GroupManagementOption == GroupManagement.CloseOnFull)
                {
                    if (currentOccupied >= board.MaxParticipants && board.CurrentStatus == BoardStatus.Open)
                    {
                        board.CurrentStatus = BoardStatus.Full;
                    }
                }
            }

            UpdateBoardStatusByCapacity(board, GetOccupiedSeatCount(board));
        }

        private static bool IsValidTag(string tag)
        {
            if (tag.StartsWith('-') || tag.EndsWith('-'))
            {
                return false;
            }

            if (tag.Any(char.IsDigit))
            {
                return false;
            }

            for (int i = 0; i < tag.Length; i++)
            {
                char c = tag[i];

                if (char.IsLetter(c))
                {
                    continue;
                }

                if (c == '-')
                {
                    if (i > 0 && tag[i - 1] == '-')
                    {
                        return false;
                    }

                    continue;
                }

                return false;
            }

            return true;
        }

        private static string FormatTag(string tag)
        {
            tag = tag.ToLowerInvariant();

            if (tag.Length > 0)
            {
                tag = char.ToUpperInvariant(tag[0]) + tag.Substring(1);
            }

            return tag;
        }
    }
}
