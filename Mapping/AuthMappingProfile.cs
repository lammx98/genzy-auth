using AutoMapper;
using Genzy.Auth.DTO;
using Genzy.Auth.Models;

namespace Genzy.Auth.Mapping;

public class AuthMappingProfile : Profile
{
    public AuthMappingProfile()
    {
        CreateMap<Account, AccountDTO>();
        CreateMap<AccountDTO, Account>();
    }
}
