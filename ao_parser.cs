using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;
using QHsm;

namespace qpc_ao_parser
{
  class AO_Parser
  {
    private string[] keywords = new string[]{
      "uint8_t", "uint16_t", "uint32_t", "uint64_t", "int8_t", "int16_t", "int32_t", "int64_t", "char",
      "QState", "QEvent", "QTimeEvt", "QActive", "QState",
      "if", "else", "else if", "return", "switch", "case", "typedef", "struct",
      "#include", "#define",
    };

    enum Parsing_States
    {
      AO_NULL,
      AO_FOUND,
    }

    private State QHandled = new State();

    private Parsing_States state = Parsing_States.AO_NULL;

    private string errorMsg = "";

    private StreamReader fileReader;
    private FileStream fileStreamer;

    private List<State> activeObject;
    private string activeObjectName;

    public string ErrorMSg
    {
      get { return errorMsg; }
    }

    public AO_Parser(string FileName)
    {
      try
      {
        fileStreamer = new FileStream(FileName, FileMode.Open);
        fileReader = new StreamReader(fileStreamer);
        activeObject = null;
      }
      catch(Exception e)
      {
        errorMsg = "Cannot open file: " + e.Message;
      }
    }

    public string AO_GetStates()
    {
      string states = "";

      foreach (State s in activeObject)
      {
        states += s.stateName + ", ";
      }

      return states;
    }

    private void ParsePossibleAO()
    {
      while (fileReader.EndOfStream == false)
      {
        string line = fileReader.ReadLine();

        /* Let's make sure this isn't a comment... */
        if (line.Contains("/*") ||
           line.Contains("*/") ||
           line.Contains("//"))
        {
          ;
        }
        else if (line.Contains("QActive"))
        {
          State top = new State();
          top.stateName = "QHsm_top";
          activeObject = new List<State>();
          activeObject.Add(top);          
        }
        else if (line.Contains('}'))
        {
          if (activeObject != null)
          {
            activeObjectName = line.Substring(2, line.Length - 3);
          }
          break;
        }
      }
    }

    private void AddState(string QStateName)
    {
      int nameLastLetterIdx = 7; /* Offset from QState word*/
      string stateName = "";
      State currentState = new State();

      /* Get the state name */
      do
      {
        stateName += QStateName[nameLastLetterIdx++];
      } while (QStateName[nameLastLetterIdx] != '(' &&
             nameLastLetterIdx < QStateName.Length);

      currentState.stateName = stateName;
      activeObject.Add(currentState);

      /* Special case - "initial state"*/
      if (currentState.stateName.Contains("initial"))
      {
        activeObject[0].transitions.Add(new Transition("Q_INIT", currentState));
      }
    }

    private string ParseSignal(string line)
    {
      string sigNam = "";

      /* Delete everything until "case" and delete that too. */
      do
      {
        sigNam += line[0];
        line = line.Substring(1);
      } while (!sigNam.Contains("case") && line.Length > 1);

      sigNam = "";
      line = line.Substring(1);

      /* Add the signal name */
      do
      {
        sigNam += line[0];
        line = line.Substring(1);
      } while (line[0] != ':' && line.Length > 1);

      return sigNam;
    }

    private void ParseTarget(Transition NewTransition, State CurrentState, string line)
    {
      string transitionTarget = "";
      do
      {
        line = line.Substring(1);
      } while (line[0] != '(' && line.Length > 1);

      line = line.Substring(2); /* Should be (&<state name>) by now*/

      do
      {
        transitionTarget += line[0];
        line = line.Substring(1);
      } while (line[0] != ')' && line.Length > 1);

      foreach (State s in activeObject)
      {
        if (transitionTarget.Contains(s.stateName))
        {
          NewTransition.targetState = s;
          CurrentState.transitions.Add(NewTransition);
          break;
        }
      }
    }

    private void ParseGuardedTarget(string Line)
    {
      string tempString = "";

      do
      {
        if (Line[0] != ' ')
        {
          tempString += Line[0];
        }
        Line = Line.Substring(1);
      } while (!(tempString.Contains("if") || tempString.Contains("else")) &&
              Line.Length > 1);


      if (!char.IsLetterOrDigit(Line[0]))
      {
        
      }
    }

    private void ParseSuperState(State CurrentState, string line)
    {
      string transitionTarget = "";
      do
      {
        line = line.Substring(1);
      } while (line[0] != '(' && line.Length > 1);

      line = line.Substring(2); /* Should be (&<state name>) by now*/

      do
      {
        transitionTarget += line[0];
        line = line.Substring(1);
      } while (line[0] != ')' && line.Length > 1);

      foreach (State s in activeObject)
      {
        if (transitionTarget.Contains(s.stateName))
        {
          CurrentState.superState = s;
          break;
        }
      }
    }

    private void ParseState(string QStateName)
    {
      int scopeLevel = 1;
      State currentState = null;
      string signal = "";

      Transition newTransition = new Transition(signal, null);
      GuardedTransition newGuardedTransition = new GuardedTransition("");

      /* Check to see if the name of this state definition is in our list. */
      foreach (State s in activeObject)
      {
        if (QStateName.Contains(s.stateName))
        {
          currentState = s;
          break;
        }
      }

      while (fileReader.EndOfStream == false && scopeLevel > 0)
      {
        string line = fileReader.ReadLine();

        if(line.Contains("{"))
        {
          scopeLevel++;
        }
        else if(line.Contains("}"))
        {
          scopeLevel--;
        }

        /* Is there a signal here? */
        if(line.Contains("case ") &&
           line.Contains(":"))
        {
          if (newTransition.transitionName != "")
          {
            newTransition.transitionName += ", ";
          }
          newTransition.transitionName += ParseSignal(line);
        }
        /* Is there a conditional statement? It might be a guarded transition. */
        else if (line.Contains("if") || line.Contains("else"))
        {
          /* Keep going until one of the transitions is foun  d. Then see if scope level went up.*/
          ParseGuardedTarget(line);
        }
        /* Is there a target? */
        else if (line.Contains("Q_TRAN"))
        {
          ParseTarget(newTransition, currentState, line);
          newTransition = new Transition("", null);
        }

        /*Is this transition just handeled?*/
        else if (line.Contains("Q_HANDLED"))
        {
          newTransition.targetState = QHandled;
          currentState.transitions.Add(newTransition);
          newTransition = new Transition("", null);
        }

        /* What is the super state?*/
        else if (line.Contains("Q_SUPER"))
        {
          ParseSuperState(currentState, line);
        }
        
        /* This might be a transition in to history. 
           TODO: To add when I can parse an AO's attributes. */
        else if (line.Contains("return "))
        {
        }
      }
    }

    /* Any state that doesn't have a transition or a super state (i.e., it wasn't event defined)
     * is pruned from the list. */
    private void PruneBadStates()
    {
      for (int i = 0; i < activeObject.Count; i++)
      {
        if (activeObject[i].transitions.Count == 0 &&
           activeObject[i].superState == null)
        {
          activeObject.RemoveAt(i);
        }
      }
    }

    public void ParseFile()
    {
      /* This parser is going through looking for QPC keywords, nothing more. */
      while (fileReader.EndOfStream == false)
      {
        string line = fileReader.ReadLine();

        if (activeObject == null)
        {
          if (line.Contains("typedef struct"))
          {
            ParsePossibleAO();
          }
        }
        else
        {
          if (line.Contains("QState"))
          {
            if (line.Contains(';'))
            {
              AddState(line);
            }
            else
            {
               ParseState(line);
            }
          }
        }
      }

      PruneBadStates();
    }
  }
}
