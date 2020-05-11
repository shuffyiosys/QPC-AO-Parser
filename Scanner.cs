using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using QHsm;

namespace QPC_AO_Parser
{
  /* Class: Scanner
     Desc:  Takes the arguments that were passed into the program and sorts them. After
            sorting the files, it scans them for keywords to turn them into events for
            the parser to consume.
   
            This is not trying to be a c scanner for a compiler. It only takes in the
            files in the argument and will not go searching for more. */
  class Scanner
  {
    private List<string> args;
    private List<string> headerFiles;
    private List<string> sourceFiles;
    private List<string[]> sourceHeaderPairs;
    private List<QEvent> eventList;

    public Scanner(string[] Args)
    {
      this.args = new List<string>();
      headerFiles = new List<string>();
      sourceFiles = new List<string>();
      sourceHeaderPairs = new List<string[]>();
      eventList = new List<QEvent>();
      args = Args.ToList();
      SeparateSourceHeaderFiles();
      GroupIncludesWithHeaderFiles();
    }

    public void ScanNextFile()
    {
      eventList = new List<QEvent>();
      if (sourceHeaderPairs.Count > 0)
      {
        if (sourceHeaderPairs[0][1] != "")
        {
          GenerateEventList(sourceHeaderPairs[0][1]);
          GenerateEventList(sourceHeaderPairs[0][0]);
        }
        else
        {
          GenerateEventList(sourceHeaderPairs[0][0]);
        }
        sourceHeaderPairs.RemoveAt(0);
      }
    }

    public List<QEvent> EventList
    {
      get { return this.eventList; }
    }

    /* @brief Separates the source and header files into the appropriate lists from the arguments.
     * 
     **/
    private void SeparateSourceHeaderFiles()
    {
      foreach (string arg in args)
      {
        if (Path.GetExtension(arg).Equals(".c"))
        {
          sourceFiles.Add(arg);
        }
        else if (Path.GetExtension(arg).Equals(".h"))
        {
          headerFiles.Add(arg);
        }
      }
    }

    /* @brief Checks which header files go with which source file.
     * 
     **/
    private void GroupIncludesWithHeaderFiles()
    {
      foreach(string source in sourceFiles)
      {
        bool foundInclude = false;
        StreamReader reader = new StreamReader(new FileStream(source, FileMode.Open, FileAccess.Read));
        sourceHeaderPairs.Add(new string[] { source, "" });

        while (!reader.EndOfStream)
        {
          string tempLine = reader.ReadLine();
          string tempWord = "";

          while (tempLine.Length > 0)
          {
            if (char.IsWhiteSpace(tempLine[0]))
            {
              if (tempWord == "#include")
              {
                foundInclude = true;
                tempWord = "";
              }
              else if (foundInclude)
              {
                tempWord = "";
                foundInclude = false;
              }
              else
              {
                tempWord = "";
              }
            }
            else
            {
              tempWord += tempLine[0];
            }
            tempLine = tempLine.Substring(1);
          }

          if (foundInclude)
          {
            /* Prune any quotes */
            for (int i = 0; i < tempWord.Length; i++)
            {
              if (tempWord[i] == '\"')
              {
                tempWord = tempWord.Remove(i, 1);
                i--;
              }
            }

            /* Check if this is in the arguments */
            foreach (string header in headerFiles)
            {
              if(tempWord.Equals(header))
              {
                sourceHeaderPairs[sourceHeaderPairs.Count - 1][1] = tempWord;
                break;
              }
            }

            tempWord = "";
            foundInclude = false;
          }
        }
        reader.Close();
        reader.Dispose();
      }
    }

    /* @brief Scans the file and generates an event list (tokens) for
     *        the parser to consume.
     **/
    private void GenerateEventList(string File)
    {
      bool inComment = false;
      StreamReader fileReader = new StreamReader(new FileStream(File, FileMode.Open, FileAccess.Read));

      while (!fileReader.EndOfStream)
      {
        string tempLine = fileReader.ReadLine();
        string tempWord = "";

        while (tempLine.Length > 0)
        {
          if (tempLine[0] == '{')
          {
            QEvent scopePlus = new QEvent();
            scopePlus.sig = (int)Signals.INCREMENT_SCOPE_LEVEL_SIG;
            eventList.Add(scopePlus);
          }
          else if (tempLine[0] == '}')
          {
            QEvent scopeMinus = new QEvent();
            scopeMinus.sig = (int)Signals.DECREMENT_SCOP_LEVEL_SIG;
            eventList.Add(scopeMinus);
          }
          else if (!char.IsWhiteSpace(tempLine[0]) && !inComment)
          {
            if (tempLine[0] == '(')
            {
              if (tempWord != "")
              {
                WordFeeder newWord = new WordFeeder(tempWord, (int)Signals.NEW_WORD_SIG);
                eventList.Add((QEvent)newWord);
                tempWord = "";
              }
              eventList.Add(new QEvent((int)Signals.START_PARENTHESIS_SIG));
            }
            else if (tempLine[0] == ')')
            {
              if (tempWord != "")
              {
                WordFeeder newWord = new WordFeeder(tempWord, (int)Signals.NEW_WORD_SIG);
                eventList.Add((QEvent)newWord);
                tempWord = "";
              }
              eventList.Add(new QEvent((int)Signals.END_PARENTHESIS_SIG));
            }
            else if (tempLine[0] != ';')
            {
              tempWord += tempLine[0];
            }
            else if (tempWord != "")
            {
              WordFeeder endLine = new WordFeeder(tempWord, (int)Signals.END_LINE_SIG);
              eventList.Add(endLine);
              tempWord = "";
            }
          }
          else if (!char.IsWhiteSpace(tempLine[0]) && inComment)
          {
            tempWord += tempLine[0];
          }
          else if (tempWord != "")
          {
            if (tempWord.Contains("/*"))
            {
              inComment = true;
            }
            else if (tempWord.Contains("*/"))
            {
              inComment = false;
            }
            else if (!inComment)
            {
              WordFeeder newWord = new WordFeeder(tempWord, (int)Signals.NEW_WORD_SIG);
              eventList.Add((QEvent)newWord);
            }
            tempWord = "";
          }

          tempLine = tempLine.Substring(1);
        }

        if (tempWord != "")
        {
          if (tempWord.Contains("/*"))
          {
            inComment = true;
          }
          else if (tempWord.Contains("*/"))
          {
            inComment = false;
          }
          else if (!inComment)
          {
            WordFeeder newWord = new WordFeeder(tempWord, (int)Signals.NEW_WORD_SIG);
            eventList.Add((QEvent)newWord);
          }
          tempWord = "";
        }
      }

      fileReader.Close();
      fileReader.Dispose();
    }
  }
}
