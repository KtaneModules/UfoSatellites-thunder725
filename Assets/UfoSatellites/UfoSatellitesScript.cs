using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System.Text.RegularExpressions;
using System;


public class UfoSatellitesScript : MonoBehaviour {

    /*
	* Triple Adjacent = ooo...
	* Two One Clockwise = oo.o..
	* Two One Counterclockwise = oo..o.
	* Triple Gap = o.o.o.
	*/
    enum SatellitePatterns	{ TripleAdjacent, TwoOneClockwise, TwoOneCounterclockwise, TripleGap};
	// 012 123 234 345 450 510
	int[] AllowedPatternsTripleAdjacent = new int[18] { 0, 1, 2, 1, 2, 3, 2, 3, 4, 3, 4, 5, 0, 4, 5, 0, 1, 5};
	// 013 124 235 340 451 502
    int[] AllowedPatternsTwoOneClock = new int[18] { 0, 1, 3, 1, 2, 4, 2, 3, 5, 0, 3, 4, 1, 4, 5, 0, 2, 5};
	// 014 125 230 341 452 503
    int[] AllowedPatternsTwoOneCounter = new int[18] { 0, 1, 4, 1, 2, 5, 0, 2, 3, 1, 3, 4, 2, 4, 5, 0, 3, 5 };
	// 024 135
	int[] AllowedPatternsTripleGap = new int[6] { 0, 2, 4, 1, 3, 5 };


    // Physical Satellites
    public KMSelectable[] Satellites;
	bool[] isSatelliteSelected;
	float[] timeAtSelection;
	public float SatelliteOrbitRotationSpeed;
	public float SatelliteSelectionRotationSpeed;
	public TextMesh[] SatelliteTexts;
	public MeshRenderer[] satelliteMeshRenderers;
	Material satelliteMaterialInstance;
	public float materialAnimationSpeed;
	public KMHighlightable[] satelliteHighlightables;
	float[] solveBeginningRotations;
    public AudioClip firstPressSound, secondPressSound, thirdPressSound, completeSound;


	// Global Variables
	float currentTime;
    // Souvenir could ask which number was NOT on a satellite (see GenerateSatelliteNumbers() for why that works)
    int[] satelliteNumbers;
	// Souvenir could ask for the sum of all numbers
	int satelliteNumbersSum;
    string concatenatedIndicators;
    char readLetter;
    SatellitePatterns validSatellitePattern;
	List<int> allSelectedSatellites;
	bool rotationsAllowed = true;

    // Bomb & Module Variables
    public KMBombInfo bombInfos;
	public KMBombModule thisBombModule;
    public KMAudio audioSystem;

    // Logging Data - Formatting & naming from Royal_Flu$h
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;


	public Material _test;


    // =-=-=-=-=-= DEFAULT FUNCTIONS =-=-=-=-=-=

    void Awake()
	{
        moduleId = moduleIdCounter++;

		// Create a new material instance, to avoid editing every UFO Satellite modules at once when one solves
		satelliteMaterialInstance = Instantiate<Material>(satelliteMeshRenderers[0].materials[1]);

		// For some reason, while you can use renderer.material = material;
		// you cannot use renderer.materials[0] = material;
		// You need to set the ENTIRE array back at once...
		// So we need to cache a reference to the material in slot 0

		Material _cacheMaterial = satelliteMeshRenderers[0].materials[0];
		var _Materials = new Material[2] { _cacheMaterial, satelliteMaterialInstance };

        // Apply that new material array to every Satellite
        foreach (var _MeshRenderer in satelliteMeshRenderers)
		{
			_MeshRenderer.materials = _Materials;
        }
    }


    // Use this for initialization
    void Start () {

		InitializeData();


		// Add delegate for each pressed Satellite.

		foreach (KMSelectable _satellite in Satellites)
		{
            _satellite.OnInteract += delegate () { SatelliteGetsPressed(_satellite, Array.IndexOf(Satellites, _satellite)); return false; };
        }


    }


	// Update is called once per frame
	void Update()
	{
        currentTime += Time.deltaTime;

        // Create a sin wave in the range 0-1
        float _sin = Mathf.Sin(currentTime * materialAnimationSpeed) * 0.5f + 0.5f;
        // Apply it to the material to animate the dots lighting up
        satelliteMaterialInstance.SetFloat("_Blend", _sin);

		// Rotations
        if (rotationsAllowed)
		{
            UpdateRotateSatellites();
        }

	}

	void UpdateRotateSatellites()
	{
        float _orbitRotation = currentTime * SatelliteOrbitRotationSpeed;
        float _selectionRotation = 0f;

        for (int i = 0; i < 6; i++)
        {
            // Compute the local "Selection Rotation" for this specific Satellite.
            if (isSatelliteSelected[i])
            {
                _selectionRotation = (currentTime - timeAtSelection[i]) * SatelliteSelectionRotationSpeed;

            }
            else
            {
                _selectionRotation = 0;
            }

            // Apply rotation as local rotation, offsetting every Satellite so it still makes a star overall
            Satellites[i].transform.localRotation = Quaternion.Euler(0, i * 60 + _orbitRotation, 0);

            // The Z-axis rotation for selection should only be applied on the "Visual"
            satelliteMeshRenderers[i].transform.localRotation = Quaternion.Euler(0, 0, _selectionRotation);
        }
    }


    // =-=-=-=-=-= INITIALIZATION & DATA SETUP =-=-=-=-=-=

	void InitializeData()
	{
        Debug.LogFormat("[UFO Satellites #{0}] Starting Initialization.", moduleId);

        currentTime = 0;

		allSelectedSatellites = new List<int> { };
        isSatelliteSelected = new bool[6];
        timeAtSelection = new float[6];
		satelliteNumbers = new int[6];

        // Concatenation of the Indicators

        ParseIndicators();

		// Append "UFO" at the beggining or the end
        AppendUfo();

		// Generate the 6 numbers that go on the satellite.
		GenerateSatelliteNumbers();

		// Read character from the Word and determine allowed Pattern
		ReadTargetCharacter();
    }

    void ParseIndicators()
	{
		// Gather all Indicators
        List<string> _AllIndicators = bombInfos.QueryWidgets(KMBombInfo.QUERYKEY_GET_INDICATOR, null);

        concatenatedIndicators = string.Empty;

		// Return immediately if we don't have any indicators.
        if (_AllIndicators.Count == 0) 
		{
            Debug.LogFormat("[UFO Satellites #{0}] No indicators found.", moduleId);
            return; 
		}

        // Sort alphabetically
        _AllIndicators.Sort();

        Debug.LogFormat("[UFO Satellites #{0}] Found {1} indicator(s).", moduleId, _AllIndicators.Count);



		string _indicatorSubstring = string.Empty;
        // Concatenate them in order
        foreach (string _currentIndicator in _AllIndicators)
		{
			_indicatorSubstring = _currentIndicator.Substring(10, 3);

            Debug.LogFormat("[UFO Satellites #{0}] Indicator with letters {1}.", moduleId, _indicatorSubstring);
            concatenatedIndicators += _indicatorSubstring;
        }

    }

	void AppendUfo()
	{
		// No need to query all ports, ask for the Parallel only.
		if (bombInfos.IsPortPresent(Port.Parallel))
		{
            // Append "UFO" to the end if Parallel is present;
            concatenatedIndicators += "UFO";

            Debug.LogFormat("[UFO Satellites #{0}] Parallel port found. Appending UFO to the end. New word is {1}.", moduleId, concatenatedIndicators);
        }
		else
		{
            // Append "UFO" to the beggining if Parallel is absent;
            concatenatedIndicators = string.Concat("UFO", concatenatedIndicators);

            Debug.LogFormat("[UFO Satellites #{0}] Parallel port not found. Appending UFO to the beggining. New word is {1}.", moduleId, concatenatedIndicators);
        }

    }

    void GenerateSatelliteNumbers()
    {
        /*
		One could just generate 6 random numbers; but for the sake of Souvenir another method will be used.
		Souvenir could give 4 numbers, 3 of which were present on Satellites, and ask which one was not present.
		For this, we need to guarantee at least 1 number that is not present within 0-9 (we have 6 satellites, so that is already done)
		and we need to guarantee that at least 3 different numbers appear.
		That is the reason behind this slightly convoluted number generation method.
		*/

        List<int> _possibleNumbers = new List<int>{0,0,1,1,2,2,3,3,4,4,5,5,6,6,7,7,8,8,9,9};


        int _pickedIndex = 0;
		for (int i = 0; i < 6; i++)
		{
			// Pick one number
			_pickedIndex = UnityEngine.Random.Range(0, _possibleNumbers.Count);

			// Apply it
            satelliteNumbers[i] = _possibleNumbers[_pickedIndex];
			SatelliteTexts[i].text = satelliteNumbers[i].ToString();

			// Increase the sum
			satelliteNumbersSum += satelliteNumbers[i];

			// Remove that picked index from the list to prevent repeats.
			// Since there are 2 of each numbers, we are guaranteed at most 3 pairs of the same digit,
			// which means we still have 3 different digits appear.
			_possibleNumbers.RemoveAt(_pickedIndex);
        }

        Debug.LogFormat("[UFO Satellites #{0}] Satellite numbers are {1}, {2}, {3}, {4}, {5} and {6}. Sum is {7}", moduleId, satelliteNumbers[0], satelliteNumbers[1], satelliteNumbers[2], satelliteNumbers[3], satelliteNumbers[4], satelliteNumbers[5], satelliteNumbersSum);
    }

	void ReadTargetCharacter()
	{
		int _length = concatenatedIndicators.Length;

		int _indexToRead = (satelliteNumbersSum-1) % _length;

        readLetter = concatenatedIndicators[_indexToRead];

        Debug.LogFormat("[UFO Satellites #{0}] Read character number {1}, meaning character {2}. That character is {3}", moduleId, satelliteNumbersSum, (_indexToRead+1), readLetter);
        Debug.LogFormat("[UFO Satellites #{0}] Valid pattern to input is:", moduleId);

        if (Regex.IsMatch(readLetter.ToString(), "[ACGLPWY]"))
		{
			validSatellitePattern = SatellitePatterns.TripleAdjacent;
            
            Debug.LogFormat("[UFO Satellites #{0}]   X X", moduleId);
            Debug.LogFormat("[UFO Satellites #{0}]  .   X", moduleId);
            Debug.LogFormat("[UFO Satellites #{0}]   . .", moduleId);
            Debug.LogFormat("[UFO Satellites #{0}] Or 'Three adjacent Satellites'.", moduleId);
        }
		else if (Regex.IsMatch(readLetter.ToString(), "[HIMNORT]"))
		{
            validSatellitePattern = SatellitePatterns.TwoOneClockwise;
            Debug.LogFormat("[UFO Satellites #{0}]   X X", moduleId);
            Debug.LogFormat("[UFO Satellites #{0}]  .   .", moduleId);
            Debug.LogFormat("[UFO Satellites #{0}]   . X", moduleId);
            Debug.LogFormat("[UFO Satellites #{0}] Or 'Two adjacent Satellites, a gap of one clockwise, then another Satellite'.", moduleId);
        }
        else if (Regex.IsMatch(readLetter.ToString(), "[BFJQUX]"))
        {
            validSatellitePattern = SatellitePatterns.TwoOneCounterclockwise;
            Debug.LogFormat("[UFO Satellites #{0}]   X X", moduleId);
            Debug.LogFormat("[UFO Satellites #{0}]  .   .", moduleId);
            Debug.LogFormat("[UFO Satellites #{0}]   X .", moduleId);
            Debug.LogFormat("[UFO Satellites #{0}] Or 'Two adjacent Satellites, a gap of two clockwise, then another Satellite'.", moduleId);
        }
		else
		{
            validSatellitePattern = SatellitePatterns.TripleGap;
            Debug.LogFormat("[UFO Satellites #{0}]   X .", moduleId);
            Debug.LogFormat("[UFO Satellites #{0}]  .   X", moduleId);
            Debug.LogFormat("[UFO Satellites #{0}]   X .", moduleId);
            Debug.LogFormat("[UFO Satellites #{0}] Or 'Three Satellites with a gap in between each of them'.", moduleId);
        }
    }





    // =-=-=-=-=-= INTERACTIONS & PLAYER FEEDBACK =-=-=-=-=-=

	void SatelliteGetsPressed(KMSelectable satelliteReference, int satelliteID)
	{
		// Safety Check
		if (satelliteReference == null) { return; }

		// Don't interfere once the module is solved
		if (moduleSolved) { return; }

		// Don't press the same button again
		if (isSatelliteSelected[satelliteID]) { return; }

		// Log
		Debug.LogFormat("[UFO Satellites #{0}] Pressed Satellite with id {1}", moduleId, satelliteID);

		// Visual Feedback on bomb
        satelliteReference.AddInteractionPunch(0.4f);

		// Remove highlightable since it can't be pressed again
		satelliteHighlightables[satelliteID].gameObject.SetActive(false);

        // Process general Selection data
        isSatelliteSelected[satelliteID] = true;
		timeAtSelection[satelliteID] = currentTime;
		allSelectedSatellites.Add(satelliteID);


        // Play sound for press 1 and 2;
        // For press 3, play Press3 if wrong or Complete if true
        switch (allSelectedSatellites.Count)
        {
            case 1:
                audioSystem.PlaySoundAtTransform(firstPressSound.name, transform);
                break;
            case 2:
                audioSystem.PlaySoundAtTransform(secondPressSound.name, transform);
                break;
            // Only verify at 3 satellites selected
            case 3:
                VerifyAnswer();
                break;
        }

    }

	void VerifyAnswer()
	{
		// We need the indices to be in order
		allSelectedSatellites.Sort();

        bool isSelectedPatternValid = false;

        // The comparison is simple
        // We have arrays called "AllowedPatternsPatternName" defined
        // Each contains 6 sets of 3 indices (or 2 in the case of TripleGap)
        // If the Selected Satellite matches any set of 3, the pattern is correct.
        // To avoid cross-indices checks, the AllowedPatterns array are sorted, and we sort the SelectedSatellites list

        switch (validSatellitePattern)
		{
			case SatellitePatterns.TripleAdjacent:

				for (int i = 0; i < 6; i ++ )
				{
					// All three numbers coincide?
					if (allSelectedSatellites[0] == AllowedPatternsTripleAdjacent[3*i] && allSelectedSatellites[1] == AllowedPatternsTripleAdjacent[1 + 3 * i] && allSelectedSatellites[2] == AllowedPatternsTripleAdjacent[2 + 3 * i])
					{
						// Mark as valid and quit the loop (no need to do useless work)
						isSelectedPatternValid = true;
						break;
					}
				}
				break;


			case SatellitePatterns.TwoOneClockwise:
                for (int i = 0; i < 6; i++)
                {
                    // All three numbers coincide?
                    if (allSelectedSatellites[0] == AllowedPatternsTwoOneClock[3 * i] && allSelectedSatellites[1] == AllowedPatternsTwoOneClock[1 + 3 * i] && allSelectedSatellites[2] == AllowedPatternsTwoOneClock[2 + 3 * i])
                    {
                        // Mark as valid and quit the loop (no need to do useless work)
                        isSelectedPatternValid = true;
                        break;
                    }
                }
                break;


			case SatellitePatterns.TwoOneCounterclockwise:
                for (int i = 0; i < 6; i++)
                {
                    // All three numbers coincide?
                    if (allSelectedSatellites[0] == AllowedPatternsTwoOneCounter[3 * i] && allSelectedSatellites[1] == AllowedPatternsTwoOneCounter[1 + 3 * i] && allSelectedSatellites[2] == AllowedPatternsTwoOneCounter[2 + 3 * i])
                    {
                        // Mark as valid and quit the loop (no need to do useless work)
                        isSelectedPatternValid = true;
                        break;
                    }
                }
                break;


			case SatellitePatterns.TripleGap:
				// Triple Gap only has two possible answers since it's very symetrical
                for (int i = 0; i < 2; i++)
                {
                    // All three numbers coincide?
                    if (allSelectedSatellites[0] == AllowedPatternsTripleGap[3 * i] && allSelectedSatellites[1] == AllowedPatternsTripleGap[1 + 3 * i] && allSelectedSatellites[2] == AllowedPatternsTripleGap[2 + 3 * i])
                    {
                        // Mark as valid and quit the loop (no need to do useless work)
                        isSelectedPatternValid = true;
                        break;
                    }
                }
                break;

			default:
                Debug.LogFormat("[UFO Satellites #{0}] Unknown state is required for validation. Please report bug to 'thunder725' in Discord with log. Solving module to avoid Soft-Locking.", moduleId);

				isSelectedPatternValid = true;
                break;
		}


		if (isSelectedPatternValid)
		{
            Debug.LogFormat("[UFO Satellites #{0}] Correct Pattern pressed.", moduleId);
            SolveModule();
        }
		else
		{
            Debug.LogFormat("[UFO Satellites #{0}] Incorrect Pattern pressed.", moduleId);
            StrikeModule();
		}
	}

	void SolveModule()
	{
        // Don't interfere once the module is solved
        if (moduleSolved) { return; }

        Debug.LogFormat("[UFO Satellites #{0}] Module Solved.", moduleId);

        moduleSolved = true;

        audioSystem.PlaySoundAtTransform(completeSound.name, transform);

        StartCoroutine(AnimateShinySolvedState());
		StartCoroutine(AnimateEndRotation());

        thisBombModule.HandlePass();
    }


	void StrikeModule()
	{
        // Don't interfere once the module is solved
        if (moduleSolved) { return; }

        Debug.LogFormat("[UFO Satellites #{0}] !!Strike!!", moduleId);

        audioSystem.PlaySoundAtTransform(thirdPressSound.name, transform);

        // Clear all "Selection" data 
        foreach (int i in allSelectedSatellites)
        {
            satelliteHighlightables[i].gameObject.SetActive(true);
        }
        allSelectedSatellites.Clear();
        isSatelliteSelected = new bool[6];

		

        thisBombModule.HandleStrike();
    }

	IEnumerator AnimateShinySolvedState()
	{
		float _shinyLerp = 0;

		while (_shinyLerp < 1)
		{
			// Prepare lerp
			_shinyLerp = Mathf.Clamp(_shinyLerp + (Time.deltaTime*0.18f), 0, 1);

			// Blend into Shiny colors!
            satelliteMaterialInstance.SetFloat("_ShinyBlend", _shinyLerp);


			// Fade out texts
			foreach (TextMesh textMesh in SatelliteTexts)
			{
				textMesh.color = Color.Lerp(Color.black, Color.clear, _shinyLerp);
			}

				yield return null;
        }

		StopCoroutine(AnimateShinySolvedState());
    }

	IEnumerator AnimateEndRotation()
	{
        // Coroutine for setting up the rotations correctly for the end

		// Start by disallowing regular rotations
        rotationsAllowed = false;

		float _tempRotationBuffer;

        // Initialize by saving the current rotations
        solveBeginningRotations = new float[12];
		for (int i = 0; i < 6; i ++)
		{
			// Axis Rotation of the satellites, the "flower shape"
			// Do some verifying so that all satellites rotate counter-clockwise to get to their target
			_tempRotationBuffer = Satellites[i].transform.localEulerAngles.y;
			if (_tempRotationBuffer < i * 60)
			{
				_tempRotationBuffer += 360;
			}
            solveBeginningRotations[i * 2] = _tempRotationBuffer + 360;

			// Z Rotation of the visuals only, the "selection rotation"
			solveBeginningRotations[1+ i * 2] = satelliteMeshRenderers[i].transform.localEulerAngles.z;
		}

		float _rotationLerp = 0;
		float _easedRotationLerp = 0;

		// Then rotate them to an ending rotation
		while (_rotationLerp < 1 )
		{
			_rotationLerp += Time.deltaTime * 0.15f;
			// Use easings because this looks prettier
			_easedRotationLerp = Easing.InOutQuad(_rotationLerp, 0, 1, 1);

			for (int i = 0;i < 6;i ++)
			{
				// Lerp using InOut Easing so it's prettier

				// This is for the "Flower Shape" rotation
				Satellites[i].transform.localRotation = Quaternion.Euler(0, Mathf.Lerp(solveBeginningRotations[i*2], i * 60, _easedRotationLerp), 0);

                // This is for the selection rotation
                satelliteMeshRenderers[i].transform.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(solveBeginningRotations[1 + i*2], 0, _easedRotationLerp));
            }

			// Wait for next frame
            yield return null;
        }

	}

    // =-=-=-=-=-= TWITCH PLAYS =-=-=-=-=-=


#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"“!{0} submit 1 5 3”. Satellites are numbered 1-6 going clockwise. Always submit three Satellites exactly.";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        // Credit to Royal_Flu$h for this line 
        var commandParts = command.ToLowerInvariant().Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        // We only accept submissions with all 3 satellites at a time
        if (commandParts.Length != 4)
        {
            yield return "sendtochaterror Only submit “!{1} submit [ThreeSatellitesNumbers]” with exactly three satellites in range 1-6.";
            yield break;
        }

        // Only accept "submit" commands
        if (commandParts[0] != "submit")
        {
            yield return "sendtochaterror Command is not “!{1} submit [ThreeSatellitesNumbers]”.";
            yield break;
        }


        // Get the ints from the command, and verify that we actually recieved 3 numbers in range 1-6
        int[] results = new int[3];

        // Internal Satellite IDs are 0-5 not 1-6 so just sneakily subtract 1 to everything
        for (int i = 0; i < 3; i++)
        {
            switch (commandParts[i + 1])
            {
                case "1":
                    results[i] = 0;
                    break;
                case "2":
                    results[i] = 1;
                    break;
                case "3":
                    results[i] = 2;
                    break;
                case "4":
                    results[i] = 3;
                    break;
                case "5":
                    results[i] = 4;
                    break;
                case "6":
                    results[i] = 5;
                    break;
                default:
                    yield return "sendtochaterror Entered numbers aren't in the range 1-6 for Satellite Numbers.";
                    yield break;
            }
                
        }

        // Verify that we didn't get any duplicate numbers
        if (results[0] == results[1] ||  results[0] == results[2] || results[1] == results[2])
        {
            yield return "sendtochaterror All three Satellite Numbers must be different.";
            yield break;
        }


        // If we land here, everything should be good
        // Tell TP to focus on the module before pressing the buttons.
        yield return null;


        // Documentation explicitely asks to use .OnInteract() and not call internal functions
        Satellites[results[0]].OnInteract();
        
        // Add some wait time to avoid sound overlap
        yield return new WaitForSeconds(.35f);
        Satellites[results[1]].OnInteract();

        yield return new WaitForSeconds(.35f);
        Satellites[results[2]].OnInteract();

    }

    // Auto-solve if Twitch Plays needs to force a solve
    IEnumerator TwitchHandleForcedSolve()
    {


        int[] pressesToSolve = new int[3];
        switch (validSatellitePattern)
        {
            case SatellitePatterns.TripleAdjacent:

                pressesToSolve = new int[3] { 0, 1, 2 };
                break;

            case SatellitePatterns.TwoOneClockwise:

                pressesToSolve = new int[3] { 0, 1, 3 };
                break;

            case SatellitePatterns.TwoOneCounterclockwise:

                pressesToSolve = new int[3] { 0, 1, 4 };
                break;

            case SatellitePatterns.TripleGap:

                pressesToSolve = new int[3] { 0, 2, 4 };
                break;
        }



        for (int i = 0; i < 3; i++)
        {
            Satellites[pressesToSolve[i]].OnInteract();
            yield return new WaitForSeconds(0.35f);
        }


    }

}
