using OpenClaw.Application.Agents;
using OpenClaw.Contracts.Agents;
using OpenClaw.Contracts.Skills;

namespace OpenClaw.Application.UnitTests.Agents;

public class PlanModeTests
{
    [Fact]
    public void PlanModeContext_Normal_ShouldAllowAllTools()
    {
        var ctx = new PlanModeContext();
        ctx.IsToolAllowed("write_file").ShouldBeTrue();
        ctx.IsToolAllowed("execute_shell").ShouldBeTrue();
        ctx.IsToolAllowed("read_file").ShouldBeTrue();
    }

    [Fact]
    public void PlanModeContext_Planning_ShouldOnlyAllowReadOnlyTools()
    {
        var ctx = new PlanModeContext { State = PlanModeState.Planning };

        ctx.IsToolAllowed("read_file").ShouldBeTrue();
        ctx.IsToolAllowed("git_status").ShouldBeTrue();
        ctx.IsToolAllowed("tool_search").ShouldBeTrue();
        ctx.IsToolAllowed("enter_plan_mode").ShouldBeTrue();
        ctx.IsToolAllowed("exit_plan_mode").ShouldBeTrue();

        ctx.IsToolAllowed("write_file").ShouldBeFalse();
        ctx.IsToolAllowed("execute_shell").ShouldBeFalse();
        ctx.IsToolAllowed("delete_file").ShouldBeFalse();
    }

    [Fact]
    public void PlanModeContext_Approved_ShouldAllowAllTools()
    {
        var ctx = new PlanModeContext { State = PlanModeState.Approved };
        ctx.IsToolAllowed("write_file").ShouldBeTrue();
        ctx.IsToolAllowed("execute_shell").ShouldBeTrue();
    }

    [Fact]
    public async Task EnterPlanModeTool_ShouldTransitionToPlanning()
    {
        var ctx = new PlanModeContext();
        var tool = new EnterPlanModeTool { Context = ctx };

        var result = await tool.ExecuteAsync(new ToolContext("""{"reason":"explore first"}"""));

        result.IsSuccess.ShouldBeTrue();
        ctx.State.ShouldBe(PlanModeState.Planning);
    }

    [Fact]
    public async Task EnterPlanModeTool_AlreadyInPlanMode_ShouldFail()
    {
        var ctx = new PlanModeContext { State = PlanModeState.Planning };
        var tool = new EnterPlanModeTool { Context = ctx };

        var result = await tool.ExecuteAsync(new ToolContext("{}"));

        result.IsSuccess.ShouldBeFalse();
        result.Error!.ShouldContain("Already in plan mode");
    }

    [Fact]
    public async Task ExitPlanModeTool_WithPlan_ShouldTransitionToApproved()
    {
        var ctx = new PlanModeContext { State = PlanModeState.Planning };
        var tool = new ExitPlanModeTool { Context = ctx };

        var result = await tool.ExecuteAsync(new ToolContext("""{"plan":"1. Read files\n2. Edit code"}"""));

        result.IsSuccess.ShouldBeTrue();
        ctx.State.ShouldBe(PlanModeState.Approved);
        ctx.Plan.ShouldBe("1. Read files\n2. Edit code");
    }

    [Fact]
    public async Task ExitPlanModeTool_WithoutPlan_ShouldFail()
    {
        var ctx = new PlanModeContext { State = PlanModeState.Planning };
        var tool = new ExitPlanModeTool { Context = ctx };

        var result = await tool.ExecuteAsync(new ToolContext("""{"plan":""}"""));

        result.IsSuccess.ShouldBeFalse();
        ctx.State.ShouldBe(PlanModeState.Planning); // unchanged
    }

    [Fact]
    public async Task ExitPlanModeTool_NotInPlanMode_ShouldFail()
    {
        var ctx = new PlanModeContext { State = PlanModeState.Normal };
        var tool = new ExitPlanModeTool { Context = ctx };

        var result = await tool.ExecuteAsync(new ToolContext("""{"plan":"my plan"}"""));

        result.IsSuccess.ShouldBeFalse();
        result.Error!.ShouldContain("Not currently in plan mode");
    }
}
