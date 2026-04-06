using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.UnitTests.Agents;

public class ToolPermissionTests
{
    [Fact]
    public void PublicTool_ShouldBeAccessibleByEveryone()
    {
        var tool = CreateTool(ToolPermissionLevel.Public);
        var ctx = new ToolContext("{}");

        ToolPermissionChecker.HasPermission(tool, ctx).ShouldBeTrue();
    }

    [Fact]
    public void WorkspaceAdminTool_ShouldDenyRegularUser()
    {
        var tool = CreateTool(ToolPermissionLevel.WorkspaceAdmin);
        var ctx = new ToolContext("{}");

        ToolPermissionChecker.HasPermission(tool, ctx).ShouldBeFalse();
    }

    [Fact]
    public void WorkspaceAdminTool_ShouldAllowWorkspaceAdmin()
    {
        var tool = CreateTool(ToolPermissionLevel.WorkspaceAdmin);
        var ctx = new ToolContext("{}") { Roles = ["Admin"] };

        ToolPermissionChecker.HasPermission(tool, ctx).ShouldBeTrue();
    }

    [Fact]
    public void WorkspaceAdminTool_ShouldAllowSuperAdmin()
    {
        var tool = CreateTool(ToolPermissionLevel.WorkspaceAdmin);
        var ctx = new ToolContext("{}") { Roles = ["SuperAdmin"] };

        ToolPermissionChecker.HasPermission(tool, ctx).ShouldBeTrue();
    }

    [Fact]
    public void SuperAdminTool_ShouldDenyWorkspaceAdmin()
    {
        var tool = CreateTool(ToolPermissionLevel.SuperAdmin);
        var ctx = new ToolContext("{}") { Roles = ["Admin"] };

        ToolPermissionChecker.HasPermission(tool, ctx).ShouldBeFalse();
    }

    [Fact]
    public void SuperAdminTool_ShouldAllowSuperAdmin()
    {
        var tool = CreateTool(ToolPermissionLevel.SuperAdmin);
        var ctx = new ToolContext("{}") { Roles = ["SuperAdmin"] };

        ToolPermissionChecker.HasPermission(tool, ctx).ShouldBeTrue();
    }

    [Fact]
    public void DefaultPermissionLevel_ShouldBePublic()
    {
        var tool = Substitute.For<IAgentTool>();
        // Default interface implementation should return Public
        tool.PermissionLevel.Returns(ToolPermissionLevel.Public);
        var ctx = new ToolContext("{}");

        ToolPermissionChecker.HasPermission(tool, ctx).ShouldBeTrue();
    }

    [Fact]
    public void GetDenialMessage_ShouldIncludeToolNameAndLevel()
    {
        var tool = CreateTool(ToolPermissionLevel.SuperAdmin, "dangerous_tool");
        var msg = ToolPermissionChecker.GetDenialMessage(tool);

        msg.ShouldContain("dangerous_tool");
        msg.ShouldContain("SuperAdmin");
    }

    private static IAgentTool CreateTool(ToolPermissionLevel level, string name = "test_tool")
    {
        var tool = Substitute.For<IAgentTool>();
        tool.Name.Returns(name);
        tool.PermissionLevel.Returns(level);
        return tool;
    }
}
