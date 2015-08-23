﻿using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace ClawAssembler
{
	public static class Parser
	{
		private static Regex commentRegex;
		private static string definitionMatchRegex;
		private static string definitionReplaceRegex;

		private static Regex dataRegex;
		private static Regex instructionRegex;
		private static Regex labelRegex;
		private static Regex defineRegex;
		private static Regex undefineRegex;

		static Parser()
		{
			// init regexes
			commentRegex = new Regex(";.*");
			definitionMatchRegex = "(?:^{SEARCH})|(?:(?<W0>[\\t ,:]){SEARCH}$)|(?:(?<W1>[\\t ,:]){REPLACE}(?<W2>[\\t ,:]))";
			definitionReplaceRegex = "${W0}${W1}{REPLACE}${W2}";

			dataRegex = new Regex("\\.[Dd][Bb](8[Uu]?|16[Uu]?|32[Uu]?|[Ss])[\\t ]*(?:\\(([\\d\\t ,\\'\\.\\-XxBb]*)\\)|\\\"([^\\\"]*)\\\")");
			instructionRegex = new Regex("^([\\w]+)[\\t ,]*([AaBbCcDd])?[\\t ,]*([AaBbCcDd]*)?$");
			labelRegex = new Regex("^([\\w]):$");
			defineRegex = new Regex("^#[Dd][Ee][Ff][Ii][Nn][Ee][\\t ]+(\\w)+(?:[\t ]+.+)?");
			undefineRegex = new Regex("^#[Uu][Nn][Dd][Ee][Ff](?:[Ii][Nn][Ee])?[\\t ]+(\\w)+");
		}

		public static CodeLine[] PreProcess(string Filename)
		{
			var codeLines = new List<CodeLine>();
			var defines = new Dictionary<string, string>();

			string mainContents = File.ReadAllText(Filename);
			mainContents = mainContents.Replace("\r\n", "\n");
			mainContents = mainContents.Replace("\\\n", "");
			mainContents = mainContents.Replace("\\n", "\n");

			string[] lines = mainContents.Split('\n');



			uint lineCount = 1;

			// Remove all whitespace and all comments
			foreach (string line in lines) {
				string trimmedLine = commentRegex.Replace(line, "").Trim();

				if (trimmedLine != "") {
					if (dataRegex.IsMatch(trimmedLine)) {
						codeLines.Add(new CodeLine(trimmedLine, lineCount, Filename){ Type = CodeLine.LineType.Data });
					} else if (instructionRegex.IsMatch(trimmedLine)) {
						codeLines.Add(new CodeLine(trimmedLine, lineCount, Filename){ Type = CodeLine.LineType.Instruction });
					} else if (labelRegex.IsMatch(trimmedLine)) {
						codeLines.Add(new CodeLine(trimmedLine, lineCount, Filename){ Type = CodeLine.LineType.Label });
					} else
						codeLines.Add(new CodeLine(trimmedLine, lineCount, Filename){ Type = CodeLine.LineType.Unknown });
				} else
					codeLines.Add(new CodeLine("", lineCount, Filename){ Type = CodeLine.LineType.Empty, Processed = true });

				lineCount++;
			}

			return codeLines.ToArray();

			//foreach (KeyValuePair<string, string> kv in Defines)
			//	line = Regex.Replace(line, definitionMatchRegex.Replace("{SEARCH}", kv.Key.Trim()), definitionReplaceRegex.Replace("{REPLACE}", kv.Value.Trim())).Trim();
		}

		/// <summary>
		/// Parse the preprocessed source code lines.
		/// </summary>
		/// <param name="CodeLines">Source code lines</param>
		public static ParserResult Parse(CodeLine[] CodeLines)
		{
			var tokens = new List<ClawToken>();
			var errors = new List<CodeError>();

			// Process all elements
			foreach (CodeLine line in CodeLines) {

				if (!line.Processed && line.Type != CodeLine.LineType.Unknown) {
					if (line.Type == CodeLine.LineType.Data) {
						Match match = dataRegex.Match(line.Content);
						string type = match.Groups[1].Value.ToUpper();
						string data = match.Groups[2].Value;
						string strval = match.Groups[3].Value;
					
						var values = new List<string>();

						// check if its not a string
						if (data != "") {
							// Trim all values and add all not empty ones to a list
							foreach (string value in data.Split(',')) {
								string trimmedValue = value.Trim();
								if (trimmedValue != "")
									values.Add(trimmedValue);
							}
						}

						if (type == "8") {
							foreach (string value in values)
								tokens.Add(new DataToken(Convert.ToSByte(value)));
						} else if (type == "8U") {
							foreach (string value in values)
								tokens.Add(new DataToken(Convert.ToByte(value)));
						} else if (type == "16") {
							foreach (string value in values)
								tokens.Add(new DataToken(Convert.ToInt16(value)));
						} else if (type == "16U") {
							foreach (string value in values)
								tokens.Add(new DataToken(Convert.ToUInt16(value)));
						} else if (type == "32") {
							foreach (string value in values)
								tokens.Add(new DataToken(Convert.ToInt32(value)));
						} else if (type == "32U") {
							foreach (string value in values)
								tokens.Add(new DataToken(Convert.ToUInt32(value)));
						} else if (type == "F") {
							foreach (string value in values)
								tokens.Add(new DataToken(Convert.ToSingle(value)));
						} else if (type == "S") {
							tokens.Add(new DataToken(strval));
						} else {
							Console.WriteLine(string.Format("ERR: Invalid data type at Line {0} in File {1}!", line.Number, line.File));
						}

						line.Processed = true;
					} else if (line.Type == CodeLine.LineType.Instruction) {
						Match match = instructionRegex.Match(line.Content);
						string mnemoric = match.Groups[1].Value.ToUpper();
						string instack = match.Groups[2].Value.ToUpper();
						string outstack = match.Groups[3].Value.ToUpper();

						ClawInstruction instruction = (ClawInstruction)Enum.Parse(typeof(ClawInstruction), mnemoric);
						ClawStack input_stack = (instack != "") ? (ClawStack)Enum.Parse(typeof(ClawStack), instack) : ClawStack.A;
						ClawStack output_stack = (outstack != "") ? (ClawStack)Enum.Parse(typeof(ClawStack), outstack) : input_stack;

						tokens.Add(new InstructionToken(instruction, input_stack, output_stack));

						line.Processed = true;
					} else if (line.Type == CodeLine.LineType.Label) {
						Match match = labelRegex.Match(line.Content);

						line.Processed = true;
					}
				}
			}

			return new ParserResult(tokens.ToArray(), CodeLines, errors.ToArray());
		}
	}
}

