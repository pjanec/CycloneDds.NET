using System;
using Xunit;

namespace DdsMonitor.Engine.Tests;

public sealed class FilterNodeTests
{
    [Fact]
    public void FilterNode_ToDynamicLinq_SimpleCondition()
    {
        var node = new FilterConditionNode
        {
            FieldPath = "Payload.Id",
            Operator = FilterComparisonOperator.Equals,
            ValueText = "42",
            ValueTypeName = typeof(int).AssemblyQualifiedName
        };

        var result = node.ToDynamicLinqString();

        Assert.Equal("Payload.Id == 42", result);
    }

    [Fact]
    public void FilterNode_ToDynamicLinq_NestedAndOr()
    {
        var root = new FilterGroupNode { Operator = FilterGroupOperator.And };
        root.Children.Add(new FilterConditionNode
        {
            FieldPath = "Payload.Id",
            Operator = FilterComparisonOperator.Equals,
            ValueText = "42",
            ValueTypeName = typeof(int).AssemblyQualifiedName
        });

        var inner = new FilterGroupNode { Operator = FilterGroupOperator.Or };
        inner.Children.Add(new FilterConditionNode
        {
            FieldPath = "TopicName",
            Operator = FilterComparisonOperator.Equals,
            ValueText = "A",
            ValueTypeName = typeof(string).AssemblyQualifiedName
        });
        inner.Children.Add(new FilterConditionNode
        {
            FieldPath = "TopicName",
            Operator = FilterComparisonOperator.Equals,
            ValueText = "B",
            ValueTypeName = typeof(string).AssemblyQualifiedName
        });

        root.Children.Add(inner);

        var result = root.ToDynamicLinqString();

        Assert.Equal("(Payload.Id == 42 and (TopicName == \"A\" or TopicName == \"B\"))", result);
    }

    [Fact]
    public void FilterNode_ToDynamicLinq_NegatedCondition()
    {
        var node = new FilterConditionNode
        {
            FieldPath = "Payload.Active",
            Operator = FilterComparisonOperator.Equals,
            ValueText = "true",
            ValueTypeName = typeof(bool).AssemblyQualifiedName,
            IsNegated = true
        };

        var result = node.ToDynamicLinqString();

        Assert.Equal("!(Payload.Active == true)", result);
    }

    [Fact]
    public void FilterNode_ToDynamicLinq_EnumValue_EmitsIntegerLiteral()
    {
        // SampleStatus.Active == 1; the LINQ expression must not contain a namespaced
        // enum identifier that Dynamic LINQ cannot resolve.
        var node = new FilterConditionNode
        {
            FieldPath = "Payload.Status",
            Operator = FilterComparisonOperator.Equals,
            ValueText = "Active",
            ValueTypeName = typeof(SampleStatus).AssemblyQualifiedName
        };

        var result = node.ToDynamicLinqString();

        // Must emit the integer value, not the fully-qualified name.
        Assert.Equal("Payload.Status == 1", result);
        Assert.DoesNotContain("DdsMonitor", result);
        Assert.DoesNotContain("SampleStatus", result);
    }

    [Fact]
    public void FilterNode_ToDynamicLinq_EnumValue_UnknownName_EmitsZero()
    {
        var node = new FilterConditionNode
        {
            FieldPath = "Payload.Status",
            Operator = FilterComparisonOperator.Equals,
            ValueText = "NotARealEnumMember",
            ValueTypeName = typeof(SampleStatus).AssemblyQualifiedName
        };

        var result = node.ToDynamicLinqString();

        Assert.Equal("Payload.Status == 0", result);
    }
}
