using System.Text;
using System.Xml;
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

        public string BuildBoardsXml(List<Board> boards)
        {
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                OmitXmlDeclaration = false,
                Encoding = Encoding.UTF8
            };

            using (var writer = XmlWriter.Create(sb, settings))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Boards");

                foreach (var b in boards)
                {
                    var visibleParticipants = b.Participants.Where(p => p.UserId != b.AuthorId).ToList();
                    var participantCount = visibleParticipants.Count + b.ExternalParticipants.Count;
                    var spotsLeft = Math.Max(b.MaxParticipants - participantCount, 0);
                    var isOpenPastDeadline = b.CurrentStatus == BoardStatus.Open && b.Deadline <= DateTimeOffset.UtcNow.UtcDateTime;

                    writer.WriteStartElement("Board");

                    writer.WriteElementString("Id", b.Id.ToString());
                    writer.WriteElementString("Title", b.Title);
                    writer.WriteElementString("Description", b.Description);
                    writer.WriteElementString("ImageUrl", string.IsNullOrWhiteSpace(b.ImageUrl) ? "/images/default-board.png" : b.ImageUrl);
                    writer.WriteElementString("Status", b.CurrentStatus.ToString());
                    writer.WriteElementString("DisplayStatus", isOpenPastDeadline ? "Open (Deadline Passed)" : b.CurrentStatus.ToString());

                    var statusClass = b.CurrentStatus switch
                    {
                        BoardStatus.Open => isOpenPastDeadline ? "status-closed" : "status-open",
                        BoardStatus.Full => "status-full",
                        BoardStatus.Closed => "status-closed",
                        BoardStatus.Cancelled => "status-cancelled",
                        BoardStatus.Archived => "status-archived",
                        _ => "status-open"
                    };
                    writer.WriteElementString("StatusClass", statusClass);

                    writer.WriteElementString("EventDate", b.EventDate.ToString("dd MMM yyyy"));
                    writer.WriteElementString("EventTime", b.EventDate.ToString("HH:mm"));
                    writer.WriteElementString("EventDateUtc", b.EventDate.ToString("o"));
                    writer.WriteElementString("Deadline", b.Deadline.ToString("dd MMM yyyy, HH:mm"));
                    writer.WriteElementString("DeadlineUtc", b.Deadline.ToString("o"));
                    writer.WriteElementString("Location", b.Location);

                    writer.WriteStartElement("Tags");
                    foreach (var tag in b.Tags)
                    {
                        writer.WriteElementString("Tag", tag.Name);
                    }
                    writer.WriteEndElement();

                    writer.WriteElementString("JoinPolicy", b.JoinPolicy.ToString());
                    writer.WriteElementString("JoinPolicyDisplay", b.JoinPolicy == BoardJoinPolicy.FirstComeFirstServe ? "First Come First Serve" : "Application");
                    writer.WriteElementString("CurrentParticipants", participantCount.ToString());
                    writer.WriteElementString("MaxParticipants", b.MaxParticipants.ToString());
                    writer.WriteElementString("SpotsLeft", spotsLeft.ToString());

                    writer.WriteStartElement("Author");
                    writer.WriteElementString("DisplayName", b.Author?.DisplayName ?? "Unknown");
                    writer.WriteElementString("ProfilePictureUrl", b.Author?.ProfilePictureUrl ?? "/images/default-profile.png");
                    writer.WriteEndElement();

                    writer.WriteStartElement("PreviewParticipants");
                    var previewParticipants = visibleParticipants.Take(5);
                    foreach (var participant in previewParticipants)
                    {
                        writer.WriteStartElement("Participant");
                        writer.WriteElementString("ProfilePictureUrl", participant.User?.ProfilePictureUrl ?? "/images/default-profile.png");
                        writer.WriteElementString("DisplayName", participant.User?.DisplayName ?? "Participant");
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();

                    writer.WriteElementString("TotalVisibleParticipants", visibleParticipants.Count.ToString());

                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }

            return sb.ToString();
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
                    System.IO.File.Delete(oldImagePath);
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
