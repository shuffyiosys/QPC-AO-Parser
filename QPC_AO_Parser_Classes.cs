using System;
using System.Collections.Generic;
using QHsm;

namespace QPC_AO_Parser 

{
  enum StateMachineType
  {
    FSM,
    HSM,
    AO,
  }

  class WordFeeder : QEvent
  {
    private string word;

    public string Word
    {
      get { return this.word; }
    }

    public WordFeeder(string Word, int Sig)
    {
      this.word = Word;
      this.sig = Sig;
    }
  }

  class Transition
  {
    public string transitionName;
    public State targetState;

    public Transition(string Name, State Target)
    {
      transitionName = Name;
      targetState = Target;
    }
  }

  class GuardedTransition
  {
    public string transitionName;
    public List<Transition> transitions; /* Note, use the name as the conditional statement.*/

    public GuardedTransition(string Name)
    {
      transitionName = Name;
      transitions = new List<Transition>();
    }
  }

  class State
  {
    public string stateName;
    public State superState;
    public List<State> subStates;
    public List<Transition> transitions;
    public List<GuardedTransition> guardedTransitions;

    public State()
    {
      stateName = "";
      superState = null;
      transitions = new List<Transition>();
      guardedTransitions = new List<GuardedTransition>();
    }

    public override string ToString()
    {
      return stateName;
    }

    /* This function is shamelessly taken from 
       http://stackoverflow.com/questions/1649027/how-do-i-print-out-a-tree-structure  
     */
    public string PrintStateChart()
    {
      return FormatStateChart("  ", true);
    }

    public string PrintTransitions()
    {
      string transitionPrint = stateName + Environment.NewLine;

      if (guardedTransitions.Count == 0 && transitions.Count == 0)
      {
        transitionPrint += "  No transitions handled by this state\r\n";
      }
      else
      {

        foreach (GuardedTransition gT in guardedTransitions)
        {
          transitionPrint += string.Format("  {0}\r\n", gT.transitionName);
          foreach (Transition t in gT.transitions)
          {
            if (t.targetState != null)
            {
              transitionPrint += string.Format("    {0} » {1}\r\n", t.transitionName, t.targetState.stateName);
            }
            else
            {
              transitionPrint += string.Format("    {0} » {1}\r\n", t.transitionName, "Handled");
            }
          }
        }

        foreach (Transition t in transitions)
        {
          if (t.targetState != null)
          {
            transitionPrint += string.Format("  {0} » {1}\r\n", t.transitionName, t.targetState.stateName);
          }
          else
          {
            transitionPrint += string.Format("  {0} » {1}\r\n", t.transitionName, "Handled");
          }
        }
      }
      return transitionPrint;
    }

    private string FormatStateChart(string Indent, bool Last)
    {
      string stateChart_str = "";

      stateChart_str += Indent;
      if (Last)
      {
        stateChart_str += "╚═";
        Indent += "  ";
      }
      else
      {
        stateChart_str += "╠═";
        Indent += "║ ";
      }
      stateChart_str += stateName + Environment.NewLine;

      for (int i = 0; i < subStates.Count; i++)
      {
        stateChart_str += subStates[i].FormatStateChart(Indent, i == subStates.Count - 1);
      }

      return stateChart_str;
    }
  }

  class ActiveObject
  {
    public string aoName;
    public StateMachineType smType;
    public List<State> states;
    public Dictionary<string, string> attributes;

    public ActiveObject(string Name)
    {
      aoName = Name;
      smType = StateMachineType.AO;
      states = new List<State>();
      attributes = new Dictionary<string, string>();
    }
  }
}
