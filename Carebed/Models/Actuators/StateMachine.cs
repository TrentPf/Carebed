using Carebed.Infrastructure.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Carebed.Models.Actuators
{
    public class StateMachine<TState> where TState : Enum
    {
        private TState _current;
        private readonly Dictionary<TState, TState[]> _transitions;

        public TState Current => _current;

        public StateMachine(TState initial, Dictionary<TState, TState[]> transitions)
        {
            _current = initial;
            _transitions = transitions;
        }

        public bool TryTransition(TState next)
        {
            if (_transitions.TryGetValue(_current, out var allowed) && allowed.Contains(next))
            {
                _current = next;
                return true;
            }
            return false;
        }
    }
}
