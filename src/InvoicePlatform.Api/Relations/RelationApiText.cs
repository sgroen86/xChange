using InvoicePlatform.Domain.Localization;

namespace InvoicePlatform.Api.Relations;

/// <summary>The problem titles of <see cref="RelationEndpoints"/>. The details come from Application.</summary>
internal interface IRelationApiText
{
    string RelationNotSaved { get; }
}

internal sealed class RelationApiTextNl : IRelationApiText
{
    public string RelationNotSaved => "Relatie niet opgeslagen";
}

internal sealed class RelationApiTextEn : IRelationApiText
{
    public string RelationNotSaved => "Customer not saved";
}

internal static class RelationApiText
{
    private static readonly IRelationApiText Nl = new RelationApiTextNl(), En = new RelationApiTextEn();

    public static IRelationApiText Current => Texts.Pick(Nl, En);
}
