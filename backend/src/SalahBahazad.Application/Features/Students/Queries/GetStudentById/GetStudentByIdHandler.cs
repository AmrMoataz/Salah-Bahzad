using Mediator;
using SalahBahazad.Application.Common.Exceptions;
using SalahBahazad.Application.Common.Interfaces;
using SalahBahazad.Application.Features.Students.DTOs;

namespace SalahBahazad.Application.Features.Students.Queries.GetStudentById;

internal sealed class GetStudentByIdHandler(IAppDbContext db)
    : IRequestHandler<GetStudentByIdQuery, StudentDetailDto>
{
    public async ValueTask<StudentDetailDto> Handle(GetStudentByIdQuery query, CancellationToken cancellationToken)
        => await StudentDetailLoader.LoadAsync(db, query.Id, cancellationToken)
           ?? throw new NotFoundException("Student", query.Id);
}
