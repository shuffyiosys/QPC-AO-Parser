using System;
using System.Collections.Generic;

namespace QPC_AO_Parser
{
  /* This creates a binary graphical representation of the state chart. Due
   * to the way QM organizes the XML attributes, it's impossible to do a single
   * pass and create all the graphical attributes necessary. i.e., you can't
   * draw a transition if you don't know where the state is, can you? */
  
  class StateBox
  {
    public string stateName;
    /* These are absolute positions. */
    public int startX;
    public int startY;
    public int endX;
    public int endY;

    public List<StateBox> childStateBoxes; 

    public StateBox(int StartX, int StartY)
    {
      stateName = "";
      startX = StartX;
      startY = StartY;
      endX = 0;
      endY = 0;
      childStateBoxes = new List<StateBox>();
    }

    public StateBox()
    {
      stateName = "";
      startX = 0;
      startY = 0;
      endX = 0;
      endY = 0;
      childStateBoxes = new List<StateBox>();
    }
  }

  class TransitionPath
  {
    public int startX;
    public int startY;
    public List<int> pathList; /* The pattern is ( startX, startY, right, up, right, up, ...)*/

    public TransitionPath()
    {
      startX = 0;
      startY = 0;
      pathList = new List<int>();
    }
  }

  class Mapper
  {
    private ActiveObject aoToMap;
    private List<StateBox> stateDiagrams;

    public Mapper(ActiveObject SourceAO)
    {
      StateBox hsmTop = new StateBox();
      hsmTop.startX = -8;
      hsmTop.startY = -8;
      aoToMap = SourceAO;
      stateDiagrams = new List<StateBox>();

      DrawAoStates(aoToMap.states[0], hsmTop, 0, 0);
    }

    private void DrawAoStates(State SourceState, StateBox SourceBox, int OffsetX, int OffsetY)
    {
      int endX = 0;
      int endY = 0;
      int offsetYModifer = 0;

      StateBox tempBox = new StateBox(SourceBox.startX + (OffsetX + 8), SourceBox.startY + (OffsetY + 8));
      for (int i = 0; i < SourceState.subStates.Count; i++)
      {
        DrawAoStates(SourceState.subStates[i], tempBox, 0, offsetYModifer);
        offsetYModifer = tempBox.childStateBoxes[i].endY;
      }

      tempBox.stateName = SourceState.stateName;

      /* Convoluted math to figure out where the end coordinates are! */
      endX = tempBox.startX + 16;
      endX += (tempBox.childStateBoxes.Count > 0) ? 16 : 0;

      endY = (tempBox.startY + ((SourceState.transitions.Count % 4) * 16));
      endY += tempBox.childStateBoxes.Count * 16;

      tempBox.endX = endX;
      tempBox.endY = endY;

      SourceBox.childStateBoxes.Add(tempBox);
    }
  }
}
