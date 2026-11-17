using System.IO;
using Ezoic.Ads.Editor;
using NUnit.Framework;

namespace Ezoic.Unity.Tests
{
    // The text transforms behind the "Include Ezoic CMP" Editor setting (Editor/EzoicCmpProjectTransforms.cs).
    // The Unity callbacks that call them (Editor/EzoicCmpBuildProcessor.cs) need a Unity Editor.
    public class GradleCmpExclusionTests
    {
        private const string Exclude = "exclude group: 'com.ezoic.sdk', module: 'ezoic-cmp-sdk'";

        private const string UnityLibrary =
            "apply plugin: 'com.android.library'\n" +
            "\n" +
            "dependencies {\n" +
            "    implementation fileTree(dir: 'libs', include: ['*.jar'])\n" +
            "// Android Resolver Dependencies Start\n" +
            "    implementation 'com.ezoic.sdk:ezoic-ads-sdk:1.24.0' // Packages/com.ezoic.ads/Editor/EzoicDependencies.xml:16\n" +
            "// Android Resolver Dependencies End\n" +
            "}\n";

        private static int Count(string text, string part)
        {
            int count = 0;
            for (int i = text.IndexOf(part, System.StringComparison.Ordinal); i >= 0; i = text.IndexOf(part, i + 1, System.StringComparison.Ordinal))
            {
                count++;
            }

            return count;
        }

        [Test]
        public void Off_AddsOneConfigurationsAllExclude_AfterTheScript()
        {
            string result = GradleCmpExclusion.Apply(UnityLibrary, includeCmp: false);

            Assert.That(result, Does.StartWith(UnityLibrary));
            Assert.That(Count(result, Exclude), Is.EqualTo(1));
            Assert.That(result, Does.Contain("configurations.all {\n    " + Exclude + "\n}\n"));
            Assert.That(GradleCmpExclusion.HasExclusion(result), Is.True);
        }

        [Test]
        public void Off_IsIdempotent()
        {
            string once = GradleCmpExclusion.Apply(UnityLibrary, includeCmp: false);

            Assert.That(GradleCmpExclusion.Apply(once, includeCmp: false), Is.EqualTo(once));
        }

        [Test]
        public void On_LeavesAScriptWithoutTheBlockUnchanged()
        {
            Assert.That(GradleCmpExclusion.Apply(UnityLibrary, includeCmp: true), Is.EqualTo(UnityLibrary));
            Assert.That(GradleCmpExclusion.HasExclusion(UnityLibrary), Is.False);
        }

        [Test]
        public void On_AfterOff_RestoresTheOriginal()
        {
            string off = GradleCmpExclusion.Apply(UnityLibrary, includeCmp: false);

            Assert.That(GradleCmpExclusion.Apply(off, includeCmp: true), Is.EqualTo(UnityLibrary));
        }

        [Test]
        public void Off_TerminatesAnUnterminatedLastLine()
        {
            string result = GradleCmpExclusion.Apply("dependencies {\n}", includeCmp: false);

            Assert.That(result, Does.StartWith("dependencies {\n}\n\n// BEGIN Ezoic CMP exclusion"));
            Assert.That(result, Does.EndWith("// END Ezoic CMP exclusion\n"));
        }

        [Test]
        public void Off_KeepsWindowsLineEndings()
        {
            string crlf = UnityLibrary.Replace("\n", "\r\n");

            string result = GradleCmpExclusion.Apply(crlf, includeCmp: false);

            Assert.That(result.Replace("\r\n", string.Empty), Does.Not.Contain("\n"));
            Assert.That(GradleCmpExclusion.Apply(result, includeCmp: false), Is.EqualTo(result));
            Assert.That(GradleCmpExclusion.Apply(result, includeCmp: true), Is.EqualTo(crlf));
        }

        [Test]
        public void Off_KotlinDsl_UsesKotlinSyntax()
        {
            string result = GradleCmpExclusion.Apply("dependencies {\n}\n", includeCmp: false, kotlinDsl: true);

            Assert.That(result, Does.Contain("    exclude(group = \"com.ezoic.sdk\", module = \"ezoic-cmp-sdk\")\n"));
            Assert.That(result, Does.Not.Contain(Exclude));
        }

        [Test]
        public void Off_ReplacesAnEditedBlock()
        {
            string edited = GradleCmpExclusion.Apply(UnityLibrary, includeCmp: false)
                .Replace(Exclude, "exclude group: 'something', module: 'else'");

            string result = GradleCmpExclusion.Apply(edited, includeCmp: false);

            Assert.That(result, Is.EqualTo(GradleCmpExclusion.Apply(UnityLibrary, includeCmp: false)));
        }
    }

    public class XcodeCmpProductTests
    {
        private static string Fixture()
        {
            return File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "unity-edm-spm.pbxproj"));
        }

        [Test]
        public void Off_SwitchesTheDistProduct_AndItsComments()
        {
            string fixture = Fixture();

            XcodeCmpProduct.Result result = XcodeCmpProduct.Apply(fixture, includeCmp: false);

            Assert.That(result.Error, Is.Null);
            Assert.That(result.DistPackageFound, Is.True);
            Assert.That(result.Switched, Is.EqualTo(1));
            Assert.That(result.AlreadyWanted, Is.EqualTo(0));
            string expected = fixture
                .Replace(
                    "7E8F9A0B1C2D3E4F5A6B7C8D /* EzoicAdsSDK */ = {\n\t\t\tisa = XCSwiftPackageProductDependency;\n\t\t\tpackage = 0F1A2B3C4D5E6F7A8B9C0D1E /* XCRemoteSwiftPackageReference \"ezoic-swift-sdk-dist\" */;\n\t\t\tproductName = EzoicAdsSDK;",
                    "7E8F9A0B1C2D3E4F5A6B7C8D /* EzoicAdsSDKWithoutCMP */ = {\n\t\t\tisa = XCSwiftPackageProductDependency;\n\t\t\tpackage = 0F1A2B3C4D5E6F7A8B9C0D1E /* XCRemoteSwiftPackageReference \"ezoic-swift-sdk-dist\" */;\n\t\t\tproductName = EzoicAdsSDKWithoutCMP;")
                .Replace("7E8F9A0B1C2D3E4F5A6B7C8D /* EzoicAdsSDK */,", "7E8F9A0B1C2D3E4F5A6B7C8D /* EzoicAdsSDKWithoutCMP */,")
                .Replace("5A1C2E3F4B5D6E7F8A9B0C1D /* EzoicAdsSDK in Frameworks */", "5A1C2E3F4B5D6E7F8A9B0C1D /* EzoicAdsSDKWithoutCMP in Frameworks */")
                .Replace("productRef = 7E8F9A0B1C2D3E4F5A6B7C8D /* EzoicAdsSDK */", "productRef = 7E8F9A0B1C2D3E4F5A6B7C8D /* EzoicAdsSDKWithoutCMP */");
            Assert.That(expected, Is.Not.EqualTo(fixture));
            Assert.That(result.Text, Is.EqualTo(expected));
        }

        [Test]
        public void Off_LeavesAProductOfTheSameNameFromAnotherPackageAlone()
        {
            string text = XcodeCmpProduct.Apply(Fixture(), includeCmp: false).Text;

            Assert.That(text, Does.Contain(
                "AD1E2F3A4B5C6D7E8F9A0B1C /* EzoicAdsSDK */ = {\n\t\t\tisa = XCSwiftPackageProductDependency;\n\t\t\tpackage = 1A2B3C4D5E6F7A8B9C0D1E2F /* XCRemoteSwiftPackageReference \"someone-elses-fork\" */;\n\t\t\tproductName = EzoicAdsSDK;"));
            Assert.That(text, Does.Contain("9C0D1E2F3A4B5C6D7E8F9A0B /* EzoicAdsSDK in Frameworks */ = {isa = PBXBuildFile; productRef = AD1E2F3A4B5C6D7E8F9A0B1C /* EzoicAdsSDK */; };"));
        }

        [Test]
        public void Off_IsIdempotent()
        {
            string once = XcodeCmpProduct.Apply(Fixture(), includeCmp: false).Text;

            XcodeCmpProduct.Result twice = XcodeCmpProduct.Apply(once, includeCmp: false);

            Assert.That(twice.Switched, Is.EqualTo(0));
            Assert.That(twice.AlreadyWanted, Is.EqualTo(1));
            Assert.That(twice.Text, Is.EqualTo(once));
        }

        [Test]
        public void On_LeavesEzoicAdsSDKAlone()
        {
            string fixture = Fixture();

            XcodeCmpProduct.Result result = XcodeCmpProduct.Apply(fixture, includeCmp: true);

            Assert.That(result.Error, Is.Null);
            Assert.That(result.Switched, Is.EqualTo(0));
            Assert.That(result.AlreadyWanted, Is.EqualTo(1));
            Assert.That(result.Text, Is.SameAs(fixture));
        }

        [Test]
        public void On_AfterOff_SwitchesBackToTheOriginal()
        {
            string fixture = Fixture();
            string off = XcodeCmpProduct.Apply(fixture, includeCmp: false).Text;

            XcodeCmpProduct.Result result = XcodeCmpProduct.Apply(off, includeCmp: true);

            Assert.That(result.Switched, Is.EqualTo(1));
            Assert.That(result.Text, Is.EqualTo(fixture));
        }

        [Test]
        public void Off_LeavesTheStandaloneCmpProductAlone()
        {
            // EDM merges com.ezoic.cmp-sdk's EzoicCmpSDK product into the same package reference.
            string fixture = Fixture().Replace(
                "/* End XCSwiftPackageProductDependency section */",
                "\t\tCE3F4A5B6C7D8E9F0A1B2C3D /* EzoicCmpSDK */ = {\n\t\t\tisa = XCSwiftPackageProductDependency;\n\t\t\tpackage = 0F1A2B3C4D5E6F7A8B9C0D1E /* XCRemoteSwiftPackageReference \"ezoic-swift-sdk-dist\" */;\n\t\t\tproductName = EzoicCmpSDK;\n\t\t};\n/* End XCSwiftPackageProductDependency section */");

            string text = XcodeCmpProduct.Apply(fixture, includeCmp: false).Text;

            Assert.That(text, Does.Contain("productName = EzoicCmpSDK;"));
            Assert.That(text, Does.Contain("CE3F4A5B6C7D8E9F0A1B2C3D /* EzoicCmpSDK */"));
        }

        [Test]
        public void Off_MatchesAQuotedProductNameAndAUrlWithoutDotGit()
        {
            string fixture = Fixture()
                .Replace("productName = EzoicAdsSDK;\n\t\t};\n\t\tAD1E", "productName = \"EzoicAdsSDK\";\n\t\t};\n\t\tAD1E")
                .Replace("ezoic-swift-sdk-dist.git\"", "ezoic-swift-sdk-dist\"");

            XcodeCmpProduct.Result result = XcodeCmpProduct.Apply(fixture, includeCmp: false);

            Assert.That(fixture, Does.Contain("productName = \"EzoicAdsSDK\";"));
            Assert.That(result.Switched, Is.EqualTo(1));
            Assert.That(result.Text, Does.Contain("productName = EzoicAdsSDKWithoutCMP;"));
            Assert.That(result.Text, Does.Not.Contain("productName = \"EzoicAdsSDK\";"));
        }

        [Test]
        public void Off_WithoutTheDistPackage_ChangesNothing()
        {
            string fixture = Fixture().Replace("ezoic/ezoic-swift-sdk-dist.git", "example/other.git");

            XcodeCmpProduct.Result result = XcodeCmpProduct.Apply(fixture, includeCmp: false);

            Assert.That(result.DistPackageFound, Is.False);
            Assert.That(result.Switched, Is.EqualTo(0));
            Assert.That(result.Text, Is.SameAs(fixture));
        }

        [Test]
        public void Malformed_ReportsAnError_AndChangesNothing()
        {
            string broken = Fixture().Replace("productName = EzoicAdsSDK;\n\t\t};\n\t\tAD1E", "productName = EzoicAdsSDK\n\t\t};\n\t\tAD1E");

            XcodeCmpProduct.Result result = XcodeCmpProduct.Apply(broken, includeCmp: false);

            Assert.That(result.Error, Does.Contain("expected ';'"));
            Assert.That(result.Switched, Is.EqualTo(0));
            Assert.That(result.Text, Is.SameAs(broken));
        }
    }
}
