using MediatR;

namespace Pharmacy.Application.Catalog.Categories.Commands.CreateCategory;

public sealed record CreateCategoryCommand(string NameEn, string NameAr, bool IsActive = true) : IRequest<Guid>;
