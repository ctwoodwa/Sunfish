namespace Sunfish.Blocks.PropertyLeasingPipeline.Models;

/// <summary>Identifier for an <see cref="Inquiry"/>.</summary>
public readonly record struct InquiryId(Guid Value);

/// <summary>Identifier for a <see cref="Prospect"/>.</summary>
public readonly record struct ProspectId(Guid Value);

/// <summary>Identifier for an <see cref="Application"/>.</summary>
public readonly record struct ApplicationId(Guid Value);

/// <summary>Identifier for an <see cref="AdverseActionNotice"/>.</summary>
public readonly record struct AdverseActionNoticeId(Guid Value);

/// <summary>Identifier for a <see cref="LeaseOffer"/>.</summary>
public readonly record struct LeaseOfferId(Guid Value);
