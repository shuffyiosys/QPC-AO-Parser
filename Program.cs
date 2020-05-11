using System;
using QHsm;
using QPC_AO_Parser;
using System.IO;

namespace qpc_ao_parser
{
  class Program
  {
    static void Main(string[] args)
    {
      AO_Parser_HSM parser;
      Scanner scanner;
      Translator translator;
      Mapper mapper;

      FileStream file;
      StreamWriter writer;

      if (args.Length == 0)
      {
        Console.WriteLine("QPC AO Parser usage: qpc_ao_parse <file name> <file name> ...");
        Console.WriteLine("e.g.,: qpc_ao_parse foobar.c foobar.h fizbaz.c");
      }
      else
      {
        try
        {
          scanner = new Scanner(args);

          do
          {
            string map = "";
            string transitions = "";
            scanner.ScanNextFile();
            parser = new AO_Parser_HSM();
            foreach (QEvent e in scanner.EventList)
            {
              parser.dispatch(e);
            }
            parser.CleanUpParser();

            foreach (ActiveObject ao in parser.parsedAOs)
            {
              file = new FileStream(ao.aoName + ".txt", FileMode.Create);
              writer = new StreamWriter(file);
              map = string.Format("{0} state chart\r\n{1}", ao.aoName, ao.states[0].PrintStateChart());
              Console.WriteLine(map);
              writer.Write(map);

              foreach (State s in ao.states)
              {
                transitions = s.PrintTransitions();
                Console.WriteLine(transitions);
                writer.Write(Environment.NewLine + transitions);
              }

              mapper = new Mapper(ao);

              translator = new Translator(ao);
              translator.StartTranslating();

              writer.Write(Environment.NewLine);
              writer.Flush();
              writer.Close();
              writer.Dispose();
            }
          } while (scanner.EventList.Count != 0) ;
        }
        catch (Exception e)
        {
          Console.WriteLine("Cannot open file: " + e.Message);
        }
      }

      Console.WriteLine("Done mapping. Press enter to quit... ");
      Console.ReadLine();
    }
  }
}