using AutoMapper;
using SmartArchivist.Application.DomainModels;
using SmartArchivist.Contract.DTOs;
using SmartArchivist.Dal.Entities;

namespace SmartArchivist.Application.Mapping
{
    /// <summary>
    /// Defines AutoMapper profile configuration for mapping between domain, contract, and data access layer document
    /// types.
    /// </summary>
    public class AutoMapperConfig : Profile
    {
        public AutoMapperConfig()
        {
            // Mapping between BL DocumentDomain and Contract DocumentDto
            CreateMap<DocumentDto, DocumentDomain>().ReverseMap();

            // Mapping between BL DocumentDomain and Contract DocumentUploadDto
            CreateMap<DocumentUploadDto, DocumentDomain>()
                .ForMember(dest => dest.Id, opt =>
                    opt.Ignore())
                .ForMember(dest => dest.Name, opt =>
                    opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.FilePath, opt =>
                    opt.Ignore())
                .ForMember(dest => dest.FileExtension, opt =>
                    opt.MapFrom(src => Path.GetExtension(src.File.FileName).ToLowerInvariant()))
                .ForMember(dest => dest.UploadDate, opt =>
                    opt.Ignore())
                .ForMember(dest => dest.ContentType, opt =>
                    opt.MapFrom(src => src.File.ContentType))
                .ForMember(dest => dest.FileSize, opt =>
                    opt.Ignore())
                .ForMember(dest => dest.State, opt =>
                    opt.Ignore())
                .ForMember(dest => dest.OcrText, opt =>
                    opt.Ignore())
                .ForMember(dest => dest.GenAiSummary, opt =>
                    opt.Ignore())
                .ForMember(dest => dest.Tags, opt =>
                    opt.Ignore());


            // Mapping between BL DocumentDomain and DAL DocumentEntity
            CreateMap<DocumentDomain, DocumentEntity>().ReverseMap();
        }
    }
}