using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using UnityEngine.UI;
using LLMUnity;
using System.Threading.Tasks;
using System.Linq;
using System;

public class Character : LLMCharacter
{
    /// <summary>
    /// The knowledge base for the character.
    /// The format of the knowledge text file should be as follows:
    /// - Each line represents a question-answer pair.
    /// - The question and answer are separated by a pipe (|) character.
    /// Example:
    /// Question1|Answer1
    /// Question2|Answer2
    /// </summary>
    [Tooltip("The knowledge base for the character. Format: Question|Answer")]
    public TextAsset characterKnowledge;

    [Tooltip("The character's image.")]
    public RawImage characterImage;

    [Tooltip("The character's name.")]
    public string characterName;

}
