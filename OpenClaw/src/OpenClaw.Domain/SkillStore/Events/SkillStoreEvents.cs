using Weda.Core.Domain;

namespace OpenClaw.Domain.SkillStore.Events;

public record SkillPublishedEvent(Guid SkillListingId, string SkillName, string Version) : IDomainEvent;

public record SkillVersionUpdatedEvent(Guid SkillListingId, string SkillName, string OldVersion, string NewVersion) : IDomainEvent;

public record SkillApprovedEvent(Guid SkillListingId, string SkillName, Guid ReviewedByUserId) : IDomainEvent;

public record SkillRejectedEvent(Guid SkillListingId, string SkillName, Guid ReviewedByUserId, string? Reason) : IDomainEvent;
