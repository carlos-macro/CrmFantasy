using System.ComponentModel.DataAnnotations;

namespace CrmFantasy.Options;

public sealed class CrmNoteOptions
{
    public const string SectionName = "CrmNotes";

    /// <summary>Usuário dono das notas criadas pelo CrmFantasy (usuário de integração).</summary>
    [Range(1, long.MaxValue)] public long OwnerId { get; set; } = 4918426000001382001;
}
