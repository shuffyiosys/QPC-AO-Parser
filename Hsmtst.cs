using System;
using QHsm;

namespace QHsm1
{
    class TestEvent : Event
    {
        public TestEvent(char c)
        {    
            switch (c & ~32)
            {
                case 'A':
                    sig = (int) TestSignals.A_SIG;
                    break;
                case 'B':
                    sig = (int)TestSignals.B_SIG;
                    break;
                case 'C':
                    sig = (int)TestSignals.C_SIG;
                    break;
                case 'D':
                    sig = (int)TestSignals.D_SIG;
                    break;
                case 'E':
                    sig = (int)TestSignals.E_SIG;
                    break;
                case 'F':
                    sig = (int)TestSignals.F_SIG;
                    break;
                case 'G':
                    sig = (int)TestSignals.G_SIG;
                    break;
                case 'H':
                    sig = (int)TestSignals.H_SIG;
                    break;
                default:
                    sig = (int)(TestSignals)HSMSignals.SUPER_SIG;
                    break;
            }
        }
    }

    enum TestSignals
    {
        A_SIG = HSMSignals.USER_SIG,
        B_SIG, C_SIG, D_SIG, E_SIG, F_SIG, G_SIG, H_SIG
    };

    class Hsmtst : Hsm
    {
        //Varibles
        private int foo;

        //CTOR
        public Hsmtst()
        {
            foo = 0;            
            init_machine(s0);
        }

        //======================================================

        private State s0(Event e)
        {
            switch (e.sig)
            {
                case (int)HSMSignals.ENTRY_SIG:
                    Console.Write("s0-ENTRY;");
                    return handled;
                case (int)HSMSignals.EXIT_SIG:
                    Console.Write("s0-EXIT;");
                    return handled;
                case (int)HSMSignals.INIT_SIG:
                    Console.Write("s0-INIT;");
                    initial_transition(s1);
                    return handled;
                case (int)TestSignals.E_SIG:
                    Console.Write("s0-E;");
                    transition(s211);
                    return handled;
                default:
                    break;
            }

            return handled;
        }

        //======================================================
        private State s1(Event e)
        {
                switch (e.sig)
                {
                    case (int)HSMSignals.ENTRY_SIG:
                        Console.Write("s1-ENTRY;");
                        return handled;
                    case (int)HSMSignals.EXIT_SIG:
                        Console.Write("s1-EXIT;");
                        return handled;
                    case (int)HSMSignals.INIT_SIG:
                        Console.Write("s1-INIT;");
                        initial_transition(s11);
                        return handled;
                    case (int)TestSignals.A_SIG:
                        Console.Write("s1-A;");
                        transition(s1);
                        return handled;
                    case (int)TestSignals.B_SIG:
                        Console.Write("s1-B;");
                        transition(s11);
                        return handled;
                    case (int)TestSignals.C_SIG:
                        Console.Write("s1-C;");
                        transition(s2);
                        return handled;
                    case (int)TestSignals.D_SIG:
                        Console.Write("s1-D;{0};", DateTime.Now);
                        transition(s0);
                        return handled;
                    case (int)TestSignals.F_SIG:
                        Console.Write("s1-F;");
                        transition(s211);
                        return handled;
                    default:
                        break;
                }

                return s0;
        }

        private State s11(Event e)
        {
            switch (e.sig)
            {
                case (int)HSMSignals.ENTRY_SIG:
                    Console.Write("s11-ENTRY;");
                    return handled;
                case (int)HSMSignals.EXIT_SIG:
                    Console.Write("s11-EXIT;");
                    return handled;
                case (int)TestSignals.G_SIG:
                    Console.Write("s11-G;");
                    transition(s211);
                    return handled;
                case (int)TestSignals.H_SIG:
                    if(foo == 1)
                    {
                        Console.Write("s11-H;");
                        foo = 0;
                        return handled;
                    }
                    break;
                default:
                    break;
            }

            return s1;
        }

        //======================================================
        
        private State s2(Event e)
        {
            switch (e.sig)
            {
                case (int)HSMSignals.ENTRY_SIG:
                    Console.Write("s2-ENTRY;");
                    return handled;
                case (int)HSMSignals.EXIT_SIG:
                    Console.Write("s2-EXIT;");
                    return handled;
                case (int)HSMSignals.INIT_SIG:
                    Console.Write("s2-INIT;");
                    initial_transition(s21);
                    return handled;
                case (int)TestSignals.C_SIG:
                    Console.Write("s2-C;");
                    transition(s1);
                    return handled;
                case (int)TestSignals.F_SIG:
                    Console.Write("s2-F;");
                    transition(s11);
                    return handled;
                default:
                    break;
            }

            return s0;
        }

        private State s21(Event e)
        {
            switch (e.sig)
            {
                case (int)HSMSignals.ENTRY_SIG:
                    Console.Write("s21-ENTRY;");
                    return handled;
                case (int)HSMSignals.EXIT_SIG:
                    Console.Write("s21-EXIT;");
                    return handled;
                case (int)HSMSignals.INIT_SIG:
                    Console.Write("s21-INIT;");
                    initial_transition(s211);
                    return handled;
                case (int)TestSignals.B_SIG:
                    Console.Write("s21-B;");
                    transition(s211);
                    return handled;
                case (int)TestSignals.H_SIG:
                    if (foo == 0)
                    {
                        Console.Write("s21-H;");
                        foo = 1;
                        transition(s21);
                        return handled;
                    }
                    break;
                default:
                    break;
            }

            return s2;
        }

        private State s211(Event e)
        {
            switch (e.sig)
            {
                case (int)HSMSignals.ENTRY_SIG:
                    Console.Write("s211-ENTRY;");
                    return handled;
                case (int)HSMSignals.EXIT_SIG:
                    Console.Write("s211-EXIT;");
                    return handled;
                case (int)TestSignals.D_SIG:
                    Console.Write("s211-D;{0};",DateTime.Now);
                    transition(s21);
                    return handled;
                case (int)TestSignals.E_SIG:
                    Console.Write("s211-E;");
                    transition(s211);
                    return handled;
                case (int)TestSignals.G_SIG:
                    Console.Write("s211-G;");
                    transition(s0);
                    return handled;
                default:
                    break;
            }

            return s21;
        }
    }
}
