using System;
using System.Collections.Generic;
using QHsm;
using System.IO;
using System.Xml;

namespace QPC_AO_Parser
{
  class Translator
  {
    private XmlWriter translateWriter;
    private ActiveObject aoToTranslate;

    public Translator(ActiveObject AO)
    {
      aoToTranslate = AO;
    }

    private void WriteAoPackage()
    {
      translateWriter.WriteStartElement("package");
        translateWriter.WriteAttributeString("name", "Active_Objects");
        translateWriter.WriteAttributeString("stereotype", "0x00");
        WriteAoClass();
      translateWriter.WriteEndElement();
    }

    private void WriteAoClass()
    {
      translateWriter.WriteStartElement("class");
        translateWriter.WriteAttributeString("name", aoToTranslate.aoName);
        switch (aoToTranslate.smType)
        {
          case StateMachineType.AO:
            translateWriter.WriteAttributeString("superclass", "qpc:QActive");
            break;
          case StateMachineType.HSM:
            translateWriter.WriteAttributeString("superclass", "qpc::QHsm");
            break;
        }

        WriteAoAttributes();
        WriteStateChart();
      translateWriter.WriteEndElement();
    }

    private void WriteAoAttributes()
    {
      foreach (KeyValuePair<string, string> entry in aoToTranslate.attributes)
      {
        translateWriter.WriteStartElement("attribute");
          translateWriter.WriteAttributeString("name", entry.Key);
          translateWriter.WriteAttributeString("type", entry.Value);
          translateWriter.WriteAttributeString("visibility", "0x00");
          translateWriter.WriteAttributeString("properties", "0x00");
        translateWriter.WriteEndElement();
      }
    }

    private void WriteStateChart()
    {
      translateWriter.WriteStartElement("statechart");
      WriteState(0, aoToTranslate.states[0]);
      translateWriter.WriteEndElement();
    }

    private void WriteState(int StateLevel, State SourceState)
    {
      if (!SourceState.Equals(aoToTranslate.states[0]))
      {
        int aoNameSize = aoToTranslate.aoName.Length + 1;
        string stateName = SourceState.stateName.Substring(aoNameSize, SourceState.stateName.Length - aoNameSize);
        translateWriter.WriteStartElement("state");
        translateWriter.WriteAttributeString("name", stateName);
      }

      WriteTransition(SourceState);
      for (int i = 0; i < SourceState.subStates.Count; i++)
      {
        WriteState(StateLevel + 1, SourceState.subStates[i]);
      }

      if (!SourceState.Equals(aoToTranslate.states[0]))
      {
        Transition dummy =  new Transition("Dummy", SourceState);
        string path = FindTargetStatePath(aoToTranslate.states[0].subStates[0], dummy);
        translateWriter.WriteStartElement("state_gylph");        
        translateWriter.WriteEndElement();
        translateWriter.WriteEndElement();
      }
    }

    private void WriteTransition(State SourceState)
    {
      foreach (Transition transition in SourceState.transitions)
      {
        if (transition.transitionName.Equals("Q_INIT_SIG"))
        {
          int i;
          string targetName = "";

          for (i = 0; i < SourceState.transitions.Count; i++)
          {
            if (transition.targetState.stateName.Equals(
               SourceState.transitions[i].targetState.stateName))
            {
              targetName = SourceState.transitions[i].targetState.stateName;
              targetName = targetName.Substring(aoToTranslate.aoName.Length + 1, targetName.Length - aoToTranslate.aoName.Length -1);
              
              break;
            }
          }

          translateWriter.WriteStartElement("initial");
          translateWriter.WriteAttributeString("target", string.Format("../{0}", i + 1));
          translateWriter.WriteEndElement();
        }
        else
        {
          translateWriter.WriteStartElement("tran");
            translateWriter.WriteAttributeString("trig", transition.transitionName);

            if (transition.targetState != null)
            {
              translateWriter.WriteAttributeString("target", FindTargetStatePath(SourceState, transition));
            }
          translateWriter.WriteEndElement();
        }
      }
    }

    /* This is essentially a neutuered version of a QP transition. The steps are:
       
       1. Build state heirarchies for both source and target states.
        1.a The heirarchies are built from top down (i.e., 0 is the target/source state)
       2. Figure out the least common ancestor (LCA) from the target's point of view.
        2.a Afterwards, populate the path string with "../"
        2.b The LCA is essentially the first state that exists in both heirarchies
       3. Go down until the target state is reached. 
        3.a Add state numbers until done.*/
    private string FindTargetStatePath(State Source, Transition SourceTransition)
    {
      string targetStatePath = "";
      int lca = 0;
      bool lcaFound = false;
      State targetState = SourceTransition.targetState;
      List<State> targetStateHeirarchy = new List<State>();
      List<State> sourceStateHeirarchy = new List<State>();
      targetStateHeirarchy.Add(SourceTransition.targetState);
      sourceStateHeirarchy.Add(Source);

      while (!targetStateHeirarchy[targetStateHeirarchy.Count - 1].stateName.Equals(aoToTranslate.states[0].stateName))
      {
        targetStateHeirarchy.Add(targetStateHeirarchy[targetStateHeirarchy.Count - 1].superState);
      }

      while (!sourceStateHeirarchy[sourceStateHeirarchy.Count - 1].stateName.Equals(aoToTranslate.states[0].stateName))
      {
        sourceStateHeirarchy.Add(sourceStateHeirarchy[sourceStateHeirarchy.Count - 1].superState);
      }

      while (lca != (sourceStateHeirarchy.Count - 1) && !lcaFound)
      {
        foreach (State target in targetStateHeirarchy)
        {
          if (target.stateName.Equals(sourceStateHeirarchy[lca].stateName))
          {
            lcaFound = true;
            break;
          }
        }
        lca++;
      }

      for (int i = 0; i < lca; i++)
      {
        targetStatePath += "../";
      }

      if (targetStatePath.Length > 0)
      {
        targetStatePath = targetStatePath.Substring(0, targetStatePath.Length - 1);
      }

      if (sourceStateHeirarchy.Count == targetStateHeirarchy.Count)
      {
        lca --;
      }
      else if (sourceStateHeirarchy.Count > targetStateHeirarchy.Count)
      {
        lca = 0;
      }
      else if ((targetStateHeirarchy.Count - sourceStateHeirarchy.Count) > 1)
      {
        lca += (targetStateHeirarchy.Count - sourceStateHeirarchy.Count) - 1; 
      }

      while (lca > 0)
      {
        for (int i = 0; i < targetStateHeirarchy[lca].subStates.Count; i++)
        {
          if (targetStateHeirarchy[lca].subStates[i].stateName.Equals(
              targetStateHeirarchy[lca - 1].stateName))
          {
            targetStatePath += string.Format("/{0}", i + 1 + DetermineStateNumber(targetStateHeirarchy[lca]));
          }
        }
        lca--;
      }

      return targetStatePath;
    }

    /* For whatever reason in QM, if a state has an initial transition and any other transition, all of its
       child state numbers go up 1. I have no idea why. This is to figure this out. */
    private int DetermineStateNumber(State InputState)
    {
      int mod = 0;
      bool hasInitSig = false;
      foreach (Transition transition in InputState.transitions)
      {
        if (transition.transitionName.Equals("Q_INIT_SIG"))
        {
          hasInitSig = true;
          break;
        }
      }

      if (InputState.guardedTransitions.Count > 0 ||
         InputState.transitions.Count > 1)
      {
        if (hasInitSig)
        {
          mod = 1;
        }
      }
      return mod;
    }

    public void StartTranslating()
    {
      XmlWriterSettings settings = new XmlWriterSettings();
      settings.Indent = true;

      using (FileStream fileStream = new FileStream(aoToTranslate.aoName + ".xml", FileMode.Create))
      using (StreamWriter sw = new StreamWriter(fileStream))
      using (translateWriter = XmlTextWriter.Create(sw, settings))
      {
        translateWriter.WriteStartDocument();
          translateWriter.WriteStartElement("model");
            translateWriter.WriteAttributeString("version", "2.1.02");
            translateWriter.WriteAttributeString("framework", "qpc");
            WriteAoPackage();
          translateWriter.WriteEndElement();
        translateWriter.WriteEndDocument();
      }
    }
  }
}
