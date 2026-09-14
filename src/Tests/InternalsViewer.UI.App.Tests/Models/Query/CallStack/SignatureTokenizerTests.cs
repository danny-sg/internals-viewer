using InternalsViewer.UI.App.Models.Query.CallStack;

namespace InternalsViewer.UI.App.Tests.Models.Query.CallStack;

public class SignatureTokenizerTests
{
    [Fact]
    public void Tokenize_Marks_The_Identifier_Before_The_Parameter_List_As_The_Name()
    {
        var signature = "CheckForBackup(RecoveryUnit *,PageId const &,int,int,int)";

        var tokens = SignatureTokenizer.Tokenize(signature);

        Assert.Equal(new SignatureToken(SignatureTokenType.Name, "CheckForBackup"), tokens[0]);
        Assert.Equal(signature, string.Concat(tokens.Select(t => t.Text)));
        Assert.Single(tokens, t => t.Type == SignatureTokenType.Name);
    }

    [Fact]
    public void Tokenize_Keeps_Scoped_Types_Whole_And_Classifies_Keywords()
    {
        var tokens = SignatureTokenizer.Tokenize("ApplyVersionOverhead(unsigned int,Page::VersionOverhead)");

        Assert.Contains(new SignatureToken(SignatureTokenType.Type, "Page::VersionOverhead"), tokens);
        Assert.Contains(new SignatureToken(SignatureTokenType.Keyword, "unsigned"), tokens);
        Assert.Contains(new SignatureToken(SignatureTokenType.Keyword, "int"), tokens);
        Assert.Contains(new SignatureToken(SignatureTokenType.Type, "RecoveryUnit"),
                        SignatureTokenizer.Tokenize("Dump(RecoveryUnit *,CDStream *,Page *)"));
    }

    [Fact]
    public void Tokenize_Names_A_Data_Member_By_Its_Last_Identifier()
    {
        var tokens = SignatureTokenizer.Tokenize("int s_count");

        Assert.Equal([SignatureTokenType.Keyword, SignatureTokenType.Punctuation, SignatureTokenType.Name],
                     tokens.Select(t => t.Type));
        Assert.Equal("s_count", tokens[2].Text);
    }

    [Theory]
    [InlineData("~Page()", "~Page")]
    [InlineData("operator=(Page const &)", "operator=")]
    [InlineData("Inner::Method(int)", "Inner::Method")]
    public void Tokenize_Treats_Destructors_Operators_And_Nested_Members_As_Names(string signature, string name)
    {
        var tokens = SignatureTokenizer.Tokenize(signature);

        Assert.Equal(new SignatureToken(SignatureTokenType.Name, name), tokens[0]);
        Assert.Equal(signature, string.Concat(tokens.Select(t => t.Text)));
    }
}
