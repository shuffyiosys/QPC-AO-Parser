using System;
using System.Collections.Generic;
using QHsm;

namespace QPC_AO_Parser
{
  enum Signals
  {
    NEW_WORD_SIG = (int)HSMSignals.USER_SIG,
    BIT_WISE_AND_SIG,
    AND_TEST_SIG,
    START_PARENTHESIS_SIG,
    END_PARENTHESIS_SIG,
    INCREMENT_SCOPE_LEVEL_SIG,
    DECREMENT_SCOP_LEVEL_SIG,
    END_LINE_SIG,
  }

  class AO_Parser_HSM : Hsm
  {
    private QState lastState;

    private State currentStateParsing = null;

    private Transition newTransition = null;

    private GuardedTransition newGuardedTransition = null;

    private Transition ifTransition = null;

    private List<string> variable = new List<string>();

    public List<ActiveObject> parsedAOs;

    private ActiveObject currentAOParsing;

    private int scopeLevel = 0;

    private int parenthesisLevel = 0;

    private string ifBlockCondition = "";

    private int ifBlockScopeLevel = 0;

    private int actionBlockScopeLevel = 0;

    public AO_Parser_HSM()
    {
      parsedAOs = new List<ActiveObject>();
      init_machine(top);
    }

    public void CleanUpParser()
    {
      PruneBadTransitions();
      PruneBadStates();
      PruneBadAOs();
    }

    /* When the parser reads finds a signal, it creates an empty transition object. If
     * for whatever reason nothing populates this, it remains empty. This will delete
     * those empty transitions. */
    private void PruneBadTransitions()
    {
      foreach (ActiveObject AO in parsedAOs)
      {
        foreach (State state in AO.states)
        {
          for (int i = 0; i < state.transitions.Count; i++)
          {
            if (state.transitions[i].transitionName == "")
            {
              state.transitions.RemoveAt(i);
              i--;
            }
          }
        }
      }
    }

    /* This is to search for states that were generated that have 0
       transitions out and were not referenced anywhere else in the 
       state machine. 
     
       NOTE - This will not work for transition into history  because the parser
              doesn't care about what happens in the signal action*/
    private void PruneBadStates()
    {
      foreach (ActiveObject AO in parsedAOs)
      {
        for (int i = 0; i < AO.states.Count; i++)
        {
          if (AO.states[i].transitions.Count == 0 &&
             AO.states[i].guardedTransitions.Count == 0)
          {
            bool isReferenced = false;
            foreach (State state in AO.states)
            {
              foreach (Transition transition in state.transitions)
              {
                if (transition.targetState != null)
                {
                  if (transition.targetState.stateName.Equals(AO.states[i].stateName))
                  {
                    isReferenced = true;
                  }
                }
              }

              if (isReferenced)
              {
                break;
              }
            }

            if (!isReferenced)
            {
              AO.states.RemoveAt(i);
              i--;
            }
          }
        }
      }
    }

    /* If an AO has no populated states, it's not a state machine. 
       This is to catch an AO that includes a state machine. The parser
       will fill it out based on forward declarations in the header
       file, but since it's not defined in the source file, it's
       left empty.*/
    private void PruneBadAOs()
    {
      for (int i = 0; i < parsedAOs.Count; i++)
      {
        if (parsedAOs[i].states.Count == 0)
        {
          parsedAOs.RemoveAt(i);
          i--;
        }
      }
    }

    /* & are the bane of my existence... */
    private string pruneAmpersand(string Input)
    {
      for (int i = 0; i < Input.Length; i++)
      {
        if (Input[i] == '&')
        {
          Input = Input.Remove(i, 1);
          i--;
        }
      }

      return Input;
    }

    private QState top(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.ENTRY_SIG:
        case (int)HSMSignals.EXIT_SIG:
          {
            return handled;
          }
          
        case (int)HSMSignals.INIT_SIG:
          {
            initial_transition(AO_Search);
            return handled;
          }
        case (int)Signals.INCREMENT_SCOPE_LEVEL_SIG:
          {
            this.scopeLevel++;
            return handled;
          }
        case (int)Signals.DECREMENT_SCOP_LEVEL_SIG:
          {
            this.scopeLevel--;
            return handled;
          }
      }
      return handled;
    }

    private QState AO_Search(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.ENTRY_SIG:
        case (int)HSMSignals.EXIT_SIG:
          {
            return handled;
          }
          
        case (int)Signals.NEW_WORD_SIG:
          {
            string tempWord = ((WordFeeder)e).Word;
            if (tempWord.Contains("typedef"))
            {
              transition(AO_Search_TypeDefFound);
            }
          }
          return handled;
      }
      return top;
    }

    private QState AO_Search_TypeDefFound(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.ENTRY_SIG:
        case (int)HSMSignals.EXIT_SIG:
          {
            return handled;
          }
          
        case (int)Signals.NEW_WORD_SIG:
          {
            string tempWord = ((WordFeeder)e).Word;

            if (tempWord.Contains("struct"))
            {
              transition(AO_Search_StructFound);
              return handled;
            }
            else
            {
              return handled;
            }
          }
      }
      return AO_Search;
    }

    private QState AO_Search_StructFound(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.ENTRY_SIG:
        case (int)HSMSignals.EXIT_SIG:
          {
            return handled;
          }

        case (int)Signals.INCREMENT_SCOPE_LEVEL_SIG:
          {
            this.scopeLevel++;

            /* Should probably start populating attributes.*/
            currentAOParsing = new ActiveObject("");
            parsedAOs.Add(currentAOParsing);
            variable = new List<string>();
            transition(AO_Search_InPossibleAO);
            return handled;
          }
      }
      return AO_Search;
    }

    private QState AO_Search_InPossibleAO(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.EXIT_SIG:
        case (int)HSMSignals.ENTRY_SIG:
          {
            
            return handled;
          }

        case (int)Signals.NEW_WORD_SIG:
          {
            string tempWord = ((WordFeeder)e).Word;

            variable.Add(tempWord);
          }
          return handled;

        case (int)Signals.END_LINE_SIG:
          {
            string tempWord = ((WordFeeder)e).Word;

            if (scopeLevel > 0)
            {
              string dataType = "";

              foreach (string s in variable)
              {
                dataType += s + " ";
              }

              dataType = dataType.Substring(0, dataType.Length - 1);

              currentAOParsing.attributes.Add(tempWord, dataType);
              variable = new List<string>();
            }
            else
            {
              bool isReallyAO = false;
              foreach (KeyValuePair<string, string> pair in currentAOParsing.attributes)
              {
                if (pair.Value.Contains("QActive"))
                {
                  currentAOParsing.smType = StateMachineType.AO;
                  currentAOParsing.attributes.Remove("super");
                  isReallyAO = true;
                  break;
                }
                else if(pair.Value.Contains("QHsm"))
                {
                  currentAOParsing.smType = StateMachineType.HSM;
                  currentAOParsing.attributes.Remove("super");
                  isReallyAO = true;
                  break;
                }
              }

              if (isReallyAO)
              {
                State QHsm_top = new State();
                QHsm_top.stateName = "QHsm_top";
                QHsm_top.superState = null;
                QHsm_top.subStates = new List<State>();

                currentAOParsing.aoName = tempWord;
                currentAOParsing.states.Add(QHsm_top);
                transition(AO_StateSearch);
              }
              else
              {
                currentAOParsing = null;
              }
            }
          }
          return handled;
      }
      return AO_Search;
    }

    private QState AO_StateSearch(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.EXIT_SIG:
        case (int)HSMSignals.ENTRY_SIG:
          {
            return handled;
          }

        case (int)Signals.NEW_WORD_SIG:
          {
            string tempWord = ((WordFeeder)e).Word;

            if (tempWord.Contains("typedef"))
            {
              transition(AO_Search_TypeDefFound);
            }
            else if (tempWord.Contains("QState") && !tempWord.Contains("QStateHandler"))
            {
              transition(AO_StateSearch_QStateFound);
            }
          }
          return handled;
      }
      return top;
    }

    private QState AO_StateSearch_QStateFound(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.EXIT_SIG:
        case (int)HSMSignals.ENTRY_SIG:
          {
            return handled;
          }

          /* Check if this is already a state we found from a forward declaration.
             If it is, then it's a definition. If not, it's a declaration so we
             add it.*/
        case (int)Signals.NEW_WORD_SIG:
          {
            bool isDecleared = false;
            string tempWord = ((WordFeeder)e).Word;

            foreach (ActiveObject ao in parsedAOs)
            {
              foreach (State s in ao.states)
              {
                if (s.stateName.Equals(tempWord))
                {
                  isDecleared = true;
                  currentAOParsing = ao;
                  currentStateParsing = s;
                }
              }
            }

            if (!isDecleared)
            {
              State newState = new State();
              newState.stateName = tempWord;
              newState.subStates = new List<State>();
              currentAOParsing.states.Add(newState);
              transition(AO_StateSearch);
            }
            else
            {
              transition(AO_StateSearch_InState);
            }
          }
          return handled;
      }
      return AO_StateSearch;
    }


    private QState AO_StateSearch_InState(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.EXIT_SIG:
        case (int)HSMSignals.ENTRY_SIG:
          {
            return handled;
          }

        case (int)Signals.DECREMENT_SCOP_LEVEL_SIG:
          {
            this.scopeLevel--;
            if (scopeLevel == 0)
            {
              transition(AO_StateSearch);

              /* This is an initial transition check, which should only
               * have return 1 Q_TRAN.*/
              if (currentStateParsing.guardedTransitions.Count == 0 &&
                 currentStateParsing.superState == null &&
                 currentStateParsing.transitions.Count == 1)
              {
                int aoCount = currentAOParsing.states.Count;
                foreach (State s in currentAOParsing.states)
                {
                  if (s.stateName == currentStateParsing.transitions[0].targetState.stateName)
                  {
                    currentAOParsing.states[0].transitions.Add(new Transition("Q_INIT_SIG", s));

                    for (int i = 0; i < currentAOParsing.states.Count; i++)
                    {
                      if (currentAOParsing.states[i] == currentStateParsing)
                      {
                        currentAOParsing.states.RemoveAt(i);
                        break;
                      }
                    }
                  }

                  if (aoCount > currentAOParsing.states.Count)
                  {
                    break;
                  }
                }
              }
            }
            return handled;
          }

        case (int)Signals.NEW_WORD_SIG:
          {
            string tempWord = ((WordFeeder)e).Word;
            if(tempWord.Contains("return"))
            {
              transition(AO_StateSearch_InState_ReturnFound);
            }
            else if (tempWord.Contains("case"))
            {
              transition(AO_StateSearch_InState_SigFound);
            }
          }
          return handled;
      }
      return AO_StateSearch;
    }


    private QState AO_StateSearch_InState_SigFound(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.EXIT_SIG:
        case (int)HSMSignals.ENTRY_SIG:
          {
            return handled;
          }

        case (int)Signals.NEW_WORD_SIG:
          {
            string tempWord = ((WordFeeder)e).Word;

            if (newTransition == null)
            {
              newTransition = new Transition(tempWord.Substring(0, tempWord.Length - 1), null);
            }
            else
            {
              newTransition.transitionName += string.Format(",{0}", tempWord.Substring(0, tempWord.Length - 1));
              
            }

            actionBlockScopeLevel = scopeLevel;
            transition(AO_StateSearch_InState_InAction);
          }
          return handled;
      }
      return AO_StateSearch_InState;
    }

    private QState AO_StateSearch_InState_InAction(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.EXIT_SIG:
        case (int)HSMSignals.ENTRY_SIG:
          {
            return handled;
          }

        case (int)Signals.NEW_WORD_SIG:
          {
            string tempWord = ((WordFeeder)e).Word;
            if (tempWord.Contains("return"))
            {
              transition(AO_StateSearch_InState_ReturnFound);
            }
            else if (tempWord.Equals("case") && actionBlockScopeLevel == scopeLevel)
            {
              transition(AO_StateSearch_InState_SigFound);
            }
            else if (tempWord.Equals("if"))
            {
              newGuardedTransition = new GuardedTransition(newTransition.transitionName);
              transition(AO_StateSearch_InState_IfBlock);
            }
          }
          return handled;
      }
      return AO_StateSearch_InState;
    }

    private QState AO_StateSearch_InState_ReturnFound(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.ENTRY_SIG:
          {
            return handled;
          }
        case (int)HSMSignals.EXIT_SIG:
        
          {
            return handled;
          }

        case (int)Signals.NEW_WORD_SIG:
          {
            string tempWord = ((WordFeeder)e).Word;

            if(tempWord.Contains("Q_TRAN"))
            {
              if (newTransition == null)
              {
                newTransition = new Transition("", null);
              }
              transition(AO_StateSearch_InState_WaitingTarget); 
            }
            else if(tempWord.Contains("Q_HANDLED"))
            {
              if (newTransition == null)
              {
                newTransition = new Transition("", null);
              }
              currentStateParsing.transitions.Add(newTransition);
              newTransition = null;
              transition(AO_StateSearch_InState); 
            }
            else if (tempWord.Contains("Q_SUPER"))
            {
              transition(AO_StateSearch_InState_WaitingSuper); 
            }
            else /* This is a transition into history... */
            {
              transition(AO_StateSearch_InState_WaitingTarget); 
            }
          }
          return handled;
      }
      return AO_StateSearch_InState;
    }

    private QState AO_StateSearch_InState_WaitingSuper(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.EXIT_SIG:
        case (int)HSMSignals.ENTRY_SIG:
          {
            return handled;
          }

        case (int)Signals.NEW_WORD_SIG:
          {
            string tempWord = ((WordFeeder)e).Word;

            tempWord = pruneAmpersand(tempWord);

            foreach (State s in currentAOParsing.states)
            {
              if (s.stateName.Equals(tempWord))
              {
                currentStateParsing.superState = s;
                s.subStates.Add(currentStateParsing);
                transition(AO_StateSearch_InState);
              }
            }
          }
          return handled;
      }
      return AO_StateSearch_InState;
    }

    private QState AO_StateSearch_InState_WaitingTarget(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.EXIT_SIG:
          {
            newTransition = null;
            return handled;
          }
        case (int)HSMSignals.ENTRY_SIG:
          {
            return handled;
          }

        case (int)Signals.NEW_WORD_SIG:
          {
            string tempWord = ((WordFeeder)e).Word;
            bool targetFound = false;

            tempWord = pruneAmpersand(tempWord);

            foreach (State s in currentAOParsing.states)
            {
              if (s.stateName.Equals(tempWord))
              {
                newTransition.targetState = s;
                currentStateParsing.transitions.Add(newTransition);
                targetFound = true;
              }
            }

            if (!targetFound)
            {
              State history = new State();
              history.stateName = "Transition To History";
              currentStateParsing.transitions.Add(new Transition(tempWord, history));
            }

            newTransition = null;
            transition(AO_StateSearch_InState);
          }
          return handled;
      }
      return AO_StateSearch_InState;
    }


    private QState AO_StateSearch_InState_IfBlock(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.ENTRY_SIG:
        case (int)HSMSignals.EXIT_SIG:
          {
            return handled;
          }

        case (int)Signals.START_PARENTHESIS_SIG:
          {
            lastState = AO_StateSearch_InState_IfBlock;
            ifBlockCondition += "if (";
            transition(AO_StateSearch_InState_IfBlock_ConditionGather);
            return handled;
          }

        case (int)Signals.END_LINE_SIG:
        case (int)Signals.NEW_WORD_SIG:
          {
            string tempWord = ((WordFeeder)e).Word;
            
            if(tempWord.Equals("else"))
            {
              ifBlockCondition += "else ";
              transition(AO_StateSearch_InState_IfBlock_Else);
            }
            else if(tempWord.Equals("break"))
            {
              if (newGuardedTransition != null)
              {
                currentStateParsing.guardedTransitions.Add(newGuardedTransition);
                newGuardedTransition = null;
              }
              newTransition = null;
              transition(AO_StateSearch_InState);
            }
            return handled;
          }
      }
      return AO_StateSearch_InState;
    }

    private QState AO_StateSearch_InState_IfBlock_Else(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.ENTRY_SIG:
        case (int)HSMSignals.EXIT_SIG:
          {
            return handled;
          }

        case (int)Signals.NEW_WORD_SIG:
          {
            string tempWord = ((WordFeeder)e).Word;
            lastState = AO_StateSearch_InState;

            if (tempWord.Equals("if"))
            {
              transition(AO_StateSearch_InState_IfBlock);
            }
            else if (tempWord.Contains("return") && ifBlockScopeLevel == scopeLevel)
            {
              ifTransition = new Transition(ifBlockCondition, null);
              transition(AO_StateSearchs_InState_GuardedTransitionReturnFound);
            }
            else
            {
              transition(AO_StateSearch_InState_IfBlock_WaitReturn);
            }
            return handled;
          }
      }
      return AO_StateSearch_InState_IfBlock;
    }

    private QState AO_StateSearch_InState_IfBlock_ConditionGather(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.ENTRY_SIG:
          {
            parenthesisLevel = 1;
            return handled;
          }

        case (int)HSMSignals.EXIT_SIG:
          {
            return handled;
          }

        case (int)Signals.START_PARENTHESIS_SIG:
          {
            ifBlockCondition += "(";
            parenthesisLevel++;
            return handled;
          }

        case (int)Signals.END_PARENTHESIS_SIG:
          {
            ifBlockCondition += ")";
            parenthesisLevel--;

            if (parenthesisLevel == 0)
            {
              ifBlockScopeLevel = scopeLevel + 1;
              transition(AO_StateSearch_InState_IfBlock_WaitReturn);
            }
            return handled;
          }

        case (int)Signals.NEW_WORD_SIG:
          {
            string tempWord = ((WordFeeder)e).Word;
            ifBlockCondition += tempWord + " ";
            return handled;
          }
      }
      return AO_StateSearch_InState_IfBlock;
    }

    private QState AO_StateSearch_InState_IfBlock_WaitReturn(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.ENTRY_SIG:
        case (int)HSMSignals.EXIT_SIG:
        case (int)Signals.START_PARENTHESIS_SIG:
        case (int)Signals.END_PARENTHESIS_SIG:
          {
            return handled;
          }

        case (int)Signals.DECREMENT_SCOP_LEVEL_SIG:
          {
            this.scopeLevel--;
            if ((ifBlockScopeLevel - 1) == scopeLevel)
            {
              ifBlockCondition = "";
              newGuardedTransition = null;
              transition(AO_StateSearch_InState);
            }
            return handled;
          }

        case (int)Signals.NEW_WORD_SIG:
          {
            string tempWord = ((WordFeeder)e).Word;
            if (tempWord.Contains("return") && ifBlockScopeLevel == scopeLevel)
            {
              ifTransition = new Transition(ifBlockCondition, null);
              transition(AO_StateSearchs_InState_GuardedTransitionReturnFound);
            }
            return handled;
          }
      }
      return AO_StateSearch_InState_IfBlock;
    }

    private QState AO_StateSearchs_InState_GuardedTransitionReturnFound(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.ENTRY_SIG:
        case (int)HSMSignals.EXIT_SIG:
          {
            return handled;
          }

        case (int)Signals.NEW_WORD_SIG:
          {
            string tempWord = ((WordFeeder)e).Word;

            if (tempWord.Contains("Q_TRAN"))
            {
              transition(AO_StateSearch_InState_GuardedTransitionWaitingTarget);
            }
            else if (tempWord.Contains("Q_HANDLED"))
            {
              newGuardedTransition.transitions.Add(ifTransition);
              ifTransition = null;
              currentStateParsing.guardedTransitions.Add(newGuardedTransition);
              transition(AO_StateSearch_InState);
            }
            else /* This is a transition into history... */
            {
              transition(AO_StateSearch_InState_GuardedTransitionWaitingTarget);
            }
          }
          return handled;
      }
      return AO_StateSearch_InState;
    }

    private QState AO_StateSearch_InState_GuardedTransitionWaitingTarget(QEvent e)
    {
      switch (e.sig)
      {
        case (int)HSMSignals.EXIT_SIG:
          {
            ifTransition = null;
            return handled;
          }
        case (int)HSMSignals.ENTRY_SIG:
        case (int)Signals.START_PARENTHESIS_SIG:
        case (int)Signals.END_PARENTHESIS_SIG:
          {
            return handled;
          }

        case (int)Signals.NEW_WORD_SIG:
          {
            string tempWord = ((WordFeeder)e).Word;
            bool targetFound = false;

            tempWord = pruneAmpersand(tempWord);

            foreach (State s in currentAOParsing.states)
            {
              if (s.stateName.Equals(tempWord))
              {
                ifTransition.targetState = s;
                newGuardedTransition.transitions.Add(ifTransition);
                targetFound = true;
              }
            }

            if (!targetFound)
            {
              State history = new State();
              history.stateName = "Transition To History";
              newGuardedTransition.transitions.Add(new Transition(tempWord, history));
            }

            ifBlockCondition = "";
            if (lastState == AO_StateSearch_InState)
            {
              currentStateParsing.guardedTransitions.Add(newGuardedTransition);
              newGuardedTransition = null;
              newTransition = null;
            }
            transition(lastState);
          }
          return handled;
      }
      return AO_StateSearch_InState_IfBlock;
    }
  }
}
