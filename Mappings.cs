using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

public static class Mappings
{
    public static UserResponse ToResponse(this User u) => new()
    {
        UserId = u.UserId,
        FullName = u.FullName,
        Email = u.Email,
        RoleId = u.RoleId,
        Role = u.Role.RoleName,
        CreatedAt = u.CreatedAt
    };

    public static CategoryResponse ToResponse(this Category c) => new()
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        CategoryName = c.CategoryName,
        DistanceKM = c.DistanceKM,
        Fee = c.Fee
    };

    public static IQueryable<EventResponse> ToResponse(this IQueryable<Event> query) =>
        query.Select(e => new EventResponse
        {
            EventId = e.EventId,
            OrganiserId = e.OrganiserId,
            OrganiserName = e.Organiser.FullName,
            Title = e.Title,
            Description = e.Description,
            EventDate = e.EventDate,
            Location = e.Location,
            DistanceKM = e.DistanceKM,
            EventType = e.EventType,
            EnrolmentCount = e.Categories
                .SelectMany(c => c.Enrolments)
                .Count(en => en.PaymentStatus != PaymentStatus.Cancelled),
            Categories = e.Categories
                .OrderBy(c => c.CategoryName)
                .Select(c => new CategoryResponse
                {
                    CategoryId = c.CategoryId,
                    EventId = c.EventId,
                    CategoryName = c.CategoryName,
                    DistanceKM = c.DistanceKM,
                    Fee = c.Fee
                })
                .ToList()
        });

    public static IQueryable<EnrolmentResponse> ToResponse(this IQueryable<Enrolment> query) =>
        query.Select(e => new EnrolmentResponse
        {
            EnrolmentId = e.EnrolmentId,
            ParticipantId = e.ParticipantId,
            ParticipantName = e.Participant.FullName,
            ParticipantEmail = e.Participant.Email,
            EventId = e.Category.EventId,
            EventTitle = e.Category.Event.Title,
            EventDate = e.Category.Event.EventDate,
            CategoryId = e.CategoryId,
            CategoryName = e.Category.CategoryName,
            EnrolmentDate = e.EnrolmentDate,
            PaymentStatus = e.PaymentStatus
        });

    public static IQueryable<ResultResponse> ToResponse(this IQueryable<RaceResult> query) =>
        query.Select(r => new ResultResponse
        {
            ResultId = r.ResultId,
            EnrolmentId = r.EnrolmentId,
            EventId = r.Enrolment.Category.EventId,
            EventTitle = r.Enrolment.Category.Event.Title,
            EventDate = r.Enrolment.Category.Event.EventDate,
            ParticipantName = r.Enrolment.Participant.FullName,
            CategoryName = r.Enrolment.Category.CategoryName,
            FinishTimeSeconds = r.FinishTimeSeconds,
            Position = r.Position,
            RecordedAt = r.RecordedAt,
            TotalFinishers = r.Enrolment.Category.Event.Categories
                .SelectMany(c => c.Enrolments)
                .Count(en => en.Result != null)
        });
}
