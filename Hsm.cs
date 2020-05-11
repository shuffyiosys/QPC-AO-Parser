using System;

namespace QHsm //Change this to the namespace the program is using.
{
    //Event signals
   public enum HSMSignals
   {
      SUPER_SIG,
      ENTRY_SIG,
      EXIT_SIG,
      INIT_SIG,
      USER_SIG //First free signal that can be used.
   };

    //Event class
   class QEvent
   {
     public int sig;

     public QEvent(int Sig)
     {
       this.sig = Sig;
     }

     public QEvent()
     {
       this.sig = (int)HSMSignals.USER_SIG;
     }
   } 

    //HSM class, intended to be inherited only (note the lack of a ctor)
   abstract class Hsm
   {
      //Variable Members

      protected delegate QState QState(QEvent e);
      protected QState source;
      protected QState state;

      //This is a placeholder for a handled state. This method should
      //never execute and will throw an exception if executed.
      protected QState handled(QEvent e)
      {
         throw new Exception(
            "ERROR: Handled state placeholder was excuted. This should never "
            + "execute as it will generate errornous results.");
      }

   //Method members
   //Public

      public void dispatch(QEvent e)
      {
         source = state;
         while (source != handled)
         {
            source = (QState)source(e);
         }
      }

   //Protected

      //Start the state machine at state s
      protected void init_machine(QState s)
      {
         if (superstate(s) == handled)
         {
            enter_state(s);
            init_my_state();
         }
      }

      protected void transition(QState target)
      {
         QState[] th = new QState[10]; //Holds target state heirarchy
         QState t;                    //Current state in heirarchy
         int lca = 0, top = 0;       //Indexes

         t = target;                 //Initialize t
         th[top] = target;           //th[0] = t

         while (th[top] != handled)   //Find target state heirarchy
         {
            if (top == 10) //Too deep, exit method.
            {
               return;
            }
            else
            {
               top++;
               t = (QState)superstate(t);
               th[top] = t;
            }

         }

         while (state != source)     //Exit states up to transition source
         {
            exit_my_state();
         }

         //Find the least common ancestor (LCA) of source and target states
         lca = 0;
         while (lca != top && th[lca] != state)
         {
            lca++;
         }

         //Exit states until a match the LCA match is found
         if (lca == top || th[lca] == target)
         {
            do
            {
               QState s = (QState)exit_my_state();
               for (lca = 0; lca != top && th[lca] != s; lca++) ;
            }
            while (lca == top);
         }

         //Enter the target state from the LCA
         while (lca != 0)
         {
            lca--;
            enter_state(th[lca]);
         }

         //Init the target state
         init_my_state();
      }

      //Go to a state upon initial transition
      protected void initial_transition(QState s)
      {
         if (state == (QState)superstate(s))
         {
            state = s;
         }
      }

      //Execute initial transition event
      protected void init_my_state()
      {
         QState s = state;
         QEvent e = new QEvent();
         e.sig = (int)HSMSignals.INIT_SIG;
         while (s(e) == handled && s != state)
         {
            s = (QState)enter_state(state);
         }
      }

      //Execute exit transition event
      protected QState exit_my_state()
      {
         QEvent e = new QEvent();
         e.sig = (int)HSMSignals.EXIT_SIG;
         state(e);
         state = (QState)superstate(state);
         return state;
      }

      //Exceute entry transition event
      protected QState enter_state(QState s)
      {
         QEvent e = new QEvent();
         e.sig = (int)HSMSignals.ENTRY_SIG;
         s(e);
         return (state = s);
      }

      //Obtain the current state's super state
      protected QState superstate(QState s)
      {
         QEvent e = new QEvent();
         e.sig = (int)HSMSignals.SUPER_SIG;
         return (s(e));
      }
   }
}
