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
        public string Name { get; set; }
        public string Category { get; set; }
        // Matches JSON: "inputs": [ { label, controlType, dataType } ]
        public List<InputDefinition> Inputs { get; set; } = new();
        public List<StepDefinitionBase> Steps { get; set; } = new();
    }
}
