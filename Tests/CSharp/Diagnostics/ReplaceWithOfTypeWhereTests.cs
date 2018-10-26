using System.Linq;
using RefactoringEssentials.CSharp.Diagnostics;
using Xunit;

namespace RefactoringEssentials.Tests.CSharp.Diagnostics
{
    public class ReplaceWithOfTypeWhereTests : CSharpDiagnosticTestBase
    {
        [Fact]
        public void TestCaseBasicWithFollowUpExpresison()
        {
            Analyze<ReplaceWithOfTypeWhereAnalyzer>(@"using System.Linq;
class Test
{
    public bool Foo(object[] obj)
    {
        $obj.Select(q => q as Test).Where(q => q != null && Foo(new object[] { q }))$;
        return true;
    }
}", @"using System.Linq;
class Test
{
    public bool Foo(object[] obj)
    {
        obj.OfType<Test>().Where(q => Foo(new object[] { q }));
        return true;
    }
}");
        }

        [Fact]
        public void TestDisable()
        {
            Analyze<ReplaceWithOfTypeWhereAnalyzer>(@"using System.Linq;
class Test
{
	public void Foo(object[] obj)
	{
		// ReSharper disable once ReplaceWithOfType.Where
		obj.Select (q => q as Test).Where (q => q != null);
	}
}");
        }

        [Fact]
        public void TestJunk()
        {
            Analyze<ReplaceWithOfTypeWhereAnalyzer>(@"using System.Linq;
class Test
{
	public void Foo(object[] obj)
	{
		obj.Select (x => q as Test).Where (q => q != null && true);
	}
}");
            Analyze<ReplaceWithOfTypeWhereAnalyzer>(@"using System.Linq;
class Test
{
	public void Foo(object[] obj)
	{
		obj.Select (q => q as Test).Where (q => 1 != null && true);
	}
}");

        }

    }
}

