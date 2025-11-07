using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RagsCore.Actions
{
    public class ConditionDefinition : StepDefinitionBase
    {
        public ConditionDefinition() : base(StepKind.Condition) { }
        public List<StepDefinitionBase> Steps { get; set; } = new();
    }
}
