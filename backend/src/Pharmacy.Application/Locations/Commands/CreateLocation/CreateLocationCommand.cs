using MediatR;
using Pharmacy.Domain.Locations;

namespace Pharmacy.Application.Locations.Commands.CreateLocation;

public sealed record CreateLocationCommand(
    string NameEn,
    string NameAr,
    LocationType Type,
    bool IsActive = true) : IRequest<Guid>;
