namespace Shared.Dtos;

using System.ComponentModel.DataAnnotations;
using Domain.Enums;
using Shared.Validation;

public record CreateFileRecordRequest(
    [Required(ErrorMessage = "O nome é obrigatório.")]
    [MinLength(1, ErrorMessage = "O nome não pode ser vazio.")]
    [MaxLength(200, ErrorMessage = "O nome não pode exceder 200 caracteres.")]
    [NotWhiteSpace(ErrorMessage = "O nome não pode ser vazio.")]
    string Name,

    [Required(ErrorMessage = "O tipo de arquivo é obrigatório.")]
    [EnumDataType(typeof(FileType), ErrorMessage = "Tipo de arquivo inválido.")]
    FileType FileType,

    int? FlopDiskNumber,

    [NotDefaultDateTime(ErrorMessage = "A data é obrigatória.")]
    DateTime Date,

    [Required(ErrorMessage = "O cliente é obrigatório.")]
    [MinLength(1, ErrorMessage = "O cliente não pode ser vazio.")]
    [MaxLength(200, ErrorMessage = "O cliente não pode exceder 200 caracteres.")]
    [NotWhiteSpace(ErrorMessage = "O cliente não pode ser vazio.")]
    string Client,

    [Range(0, 99_999_999_999, ErrorMessage = "O número do arquivo deve ser um inteiro entre 0 e 99999999999.")]
    long FileNumber = 0
);
