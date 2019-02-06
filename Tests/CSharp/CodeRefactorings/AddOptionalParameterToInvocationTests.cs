using RefactoringEssentials.CSharp.CodeRefactorings;
using Xunit;

namespace RefactoringEssentials.Tests.CSharp.CodeRefactorings
{
    public class AddOptionalParameterToInvocationTests : CSharpCodeRefactoringTestBase
    {
        [Fact]
        public void TestSimple()
        {
            Test<AddOptionalParameterToInvocationCodeRefactoringProvider>(@"
class TestClass
{
    public void Foo(string msg = ""Hello"") {}
    public void Bar() {
        $Foo();
    }
}", @"
class TestClass
{
    public void Foo(string msg = ""Hello"") {}
    public void Bar() {
        Foo(""Hello"");
    }
}");
        }

        [Fact]
        public void TestSimpleWithComment()
        {
            Test<AddOptionalParameterToInvocationCodeRefactoringProvider>(@"
class TestClass
{
    public void Foo(string msg = ""Hello"") {}
    public void Bar() {
        // Some comment
        $Foo();
    }
}", @"
class TestClass
{
    public void Foo(string msg = ""Hello"") {}
    public void Bar() {
        // Some comment
        Foo(""Hello"");
    }
}");
        }

        [Fact]
        public void TestMultiple1()
        {
            Test<AddOptionalParameterToInvocationCodeRefactoringProvider>(@"
class TestClass
{
    public void Foo(string msg = ""Hello"", string msg2 = ""Bar"") {}
    public void Bar() {
        $Foo();
    }
}", @"
class TestClass
{
    public void Foo(string msg = ""Hello"", string msg2 = ""Bar"") {}
    public void Bar() {
        Foo(""Hello"");
    }
}");
        }

        [Fact]
        public void TestExtensionMethod()
        {
            Test<AddOptionalParameterToInvocationCodeRefactoringProvider>(@"
static class Extensions
{
    public static void Foo(this string self, string msg = ""Hello"") {}
}
class TestClass
{
    public void Bar() {
        ""test"".$Foo();
    }
}", @"
static class Extensions
{
    public static void Foo(this string self, string msg = ""Hello"") {}
}
class TestClass
{
    public void Bar() {
        ""test"".Foo(""Hello"");
    }
}");
        }

        [Fact]
        public void TestExtensionMethod2()
        {
            Test<AddOptionalParameterToInvocationCodeRefactoringProvider>(@"
static class Extensions
{
    public static void Foo(this string self, string a, string msg = ""Hello"") {}
}
class TestClass
{
    public void Bar() {
        ""test"".$Foo(""thing"");
    }
}", @"
static class Extensions
{
    public static void Foo(this string self, string a, string msg = ""Hello"") {}
}
class TestClass
{
    public void Bar() {
        ""test"".Foo(""thing"", ""Hello"");
    }
}");
        }

        [Fact]
        public void TestExtensionMethodNonReducedForm()
        {
            Test<AddOptionalParameterToInvocationCodeRefactoringProvider>(@"
static class Extensions
{
    public static void Foo(this string self, string msg = ""Hello"") {}
}
class TestClass
{
    public void Bar() {
        Extensions.$Foo (""test"");
    }
}", @"
static class Extensions
{
    public static void Foo(this string self, string msg = ""Hello"") {}
}
class TestClass
{
    public void Bar() {
        Extensions.Foo(""test"", ""Hello"");
    }
}");
        }

        [Fact]
        public void TestExtensionMethodNonReducedForm2()
        {
            Test<AddOptionalParameterToInvocationCodeRefactoringProvider>(@"
static class Extensions
{
    public static void Foo(this string self, string a, string msg = ""Hello"") {}
}
class TestClass
{
    public void Bar() {
        Extensions.$Foo (""test"", ""thing"");
    }
}", @"
static class Extensions
{
    public static void Foo(this string self, string a, string msg = ""Hello"") {}
}
class TestClass
{
    public void Bar() {
        Extensions.Foo(""test"", ""thing"", ""Hello"");
    }
}");
        }

        [Fact]
        public void TestMultiple2()
        {
            Test<AddOptionalParameterToInvocationCodeRefactoringProvider>(@"
class TestClass
{
    public void Foo(string msg = ""Hello"", string msg2 = ""Bar"") {}
    public void Bar() {
        $Foo();
    }
}", @"
class TestClass
{
    public void Foo(string msg = ""Hello"", string msg2 = ""Bar"") {}
    public void Bar() {
        Foo(msg2: ""Bar"");
    }
}", 1);
        }

        [Fact]
        public void TestMultiple3()
        {
            Test<AddOptionalParameterToInvocationCodeRefactoringProvider>(@"
class TestClass
{
    public void Foo(string msg = ""Hello"", string msg2 = ""Bar"") { }
    public void Bar()
    {
        $Foo();
    }
}", @"
class TestClass
{
    public void Foo(string msg = ""Hello"", string msg2 = ""Bar"") { }
    public void Bar()
    {
        Foo(""Hello"", ""Bar"");
    }
}", 2);
        }

        [Fact]
        public void TestNoMoreParameters()
        {
            TestWrongContext<AddOptionalParameterToInvocationCodeRefactoringProvider>(@"
class TestClass
{
    public void Foo(string msg = ""Hello"", string msg2 = ""Bar"") {}
    public void Bar() {
        $Foo(string.Empty, string.Empty);
    }
}
");
        }

        [Fact]
        public void TestParams()
        {
            TestWrongContext<AddOptionalParameterToInvocationCodeRefactoringProvider>(@"
class TestClass
{
    public void Foo(params string[] p) {}
    public void Bar()
    {
        $Foo();
    }
}
");
        }
    }
}
