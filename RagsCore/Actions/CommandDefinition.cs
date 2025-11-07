using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RagsCore.Actions
{
    // Remove duplicate (shadowing) properties; inherit Name/Category/Inputs from base
    public class CommandDefinition : StepDefinitionBase
    {
        public CommandDefinition() : base(StepKind.Command) { }
    }
}
