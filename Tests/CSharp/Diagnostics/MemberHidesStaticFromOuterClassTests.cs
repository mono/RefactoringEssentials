using RefactoringEssentials.CSharp.Diagnostics;
using Xunit;

namespace RefactoringEssentials.Tests.CSharp.Diagnostics
{
    public class MemberHidesStaticFromOuterClassTests : CSharpDiagnosticTestBase
    {
        [Fact]
        public void TestProperty()
        {
            Analyze<MemberHidesStaticFromOuterClassAnalyzer>(@"
public class Foo
{
	public class Bar
	{
        public void $Method$() {}
		public string $Test$ { get; set; }
        public string $Field$;
        public event EventHandler $FieldEvent$;
        public event EventHandler $PropertyEvent$ {
            add {}
            remove {}
        }
	}

	public static string Test { get; set; }
    public static void Method() {}
    public static string Field;
    public static event EventHandler FieldEvent;
    public static event EventHandler PropertyEvent {
        add {}
        remove {}
    }
}
");
        }


        [Fact]
        public void TestDisable()
        {
            Analyze<MemberHidesStaticFromOuterClassAnalyzer>(@"
public class Foo
{
	public class Bar
	{
#pragma warning disable " + CSharpDiagnosticIDs.MemberHidesStaticFromOuterClassAnalyzerID + @"
		public string Test { get; set; }
	}

	public static string Test { get; set; }
}
");
        }


    }
}

