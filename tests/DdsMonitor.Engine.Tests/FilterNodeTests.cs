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
}
