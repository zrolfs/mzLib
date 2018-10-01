using System;
using System.Collections.Generic;
using System.Linq;
using Proteomics.Fragmentation;

namespace Proteomics.ProteolyticDigestion
{
    public class Protease
    {
        public Protease(string name, IEnumerable<Tuple<string, FragmentationTerminus>> sequencesInducingCleavage, IEnumerable<Tuple<string, FragmentationTerminus>> sequencesPreventingCleavage, CleavageSpecificity cleavageSpecificity, string psiMSAccessionNumber, string psiMSName, string siteRegexp)
        {
            Name = name;
            SequencesInducingCleavage = sequencesInducingCleavage;
            SequencesPreventingCleavage = sequencesPreventingCleavage;
            CleavageSpecificity = cleavageSpecificity;
            PsiMsAccessionNumber = psiMSAccessionNumber;
            PsiMsName = psiMSName;
            SiteRegexp = siteRegexp;
        }

        public string Name { get; }
        public FragmentationTerminus CleavageTerminus { get; }
        public IEnumerable<Tuple<string, FragmentationTerminus>> SequencesInducingCleavage { get; }
        public IEnumerable<Tuple<string, FragmentationTerminus>> SequencesPreventingCleavage { get; }
        public CleavageSpecificity CleavageSpecificity { get; }
        public string PsiMsAccessionNumber { get; }
        public string PsiMsName { get; }
        public string SiteRegexp { get; }

        public override string ToString()
        {
            return Name;
        }

        public override bool Equals(object obj)
        {
            var a = obj as Protease;
            return a != null
                && a.Name.Equals(Name);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        /// <summary>
        /// Gets intervals of a protein sequence that will result from digestion by this protease.
        /// </summary>
        /// <param name="protein"></param>
        /// <param name="maximumMissedCleavages"></param>
        /// <param name="initiatorMethionineBehavior"></param>
        /// <param name="minPeptidesLength"></param>
        /// <param name="maxPeptidesLength"></param>
        /// <returns></returns>
        internal List<ProteolyticPeptide> GetDigestionIntervals(Protein protein, int maximumMissedCleavages, InitiatorMethionineBehavior initiatorMethionineBehavior,
            int minPeptidesLength, int maxPeptidesLength)
        {
            List<ProteolyticPeptide> peptides = new List<ProteolyticPeptide>();

            // proteolytic cleavage in one spot
            if (CleavageSpecificity == CleavageSpecificity.SingleN || CleavageSpecificity == CleavageSpecificity.SingleC)
            {
                bool maxTooBig = protein.Length + maxPeptidesLength < 0; //when maxPeptidesLength is too large, it becomes negative and causes issues
                for (int proteinStart = 1; proteinStart <= protein.Length; proteinStart++)
                {
                    if (CleavageSpecificity == CleavageSpecificity.SingleN && OkayMinLength(protein.Length - proteinStart + 1, minPeptidesLength))
                    {
                        //need Math.Max if max length is int.MaxLength, since +proteinStart will make it negative
                        peptides.Add(new ProteolyticPeptide(protein, proteinStart, maxTooBig ? protein.Length : Math.Min(protein.Length, proteinStart + maxPeptidesLength), 0, CleavageSpecificity.SingleN, "SingleN"));
                    }

                    if (CleavageSpecificity == CleavageSpecificity.SingleC && OkayMinLength(proteinStart, minPeptidesLength))
                    {
                        peptides.Add(new ProteolyticPeptide(protein, Math.Max(1, proteinStart - maxPeptidesLength), proteinStart, 0, CleavageSpecificity.SingleC, "SingleC"));
                    }
                }
            }
            //top-down
            else if (CleavageSpecificity == CleavageSpecificity.None)
            {
                // retain methionine
                if ((initiatorMethionineBehavior != InitiatorMethionineBehavior.Cleave || protein[0] != 'M')
                    && OkayLength(protein.Length, minPeptidesLength, maxPeptidesLength))
                {
                    peptides.Add(new ProteolyticPeptide(protein, 1, protein.Length, 0, CleavageSpecificity.Full, "full"));
                }

                // cleave methionine
                if ((initiatorMethionineBehavior != InitiatorMethionineBehavior.Retain && protein[0] == 'M')
                    && OkayLength(protein.Length - 1, minPeptidesLength, maxPeptidesLength))
                {
                    peptides.Add(new ProteolyticPeptide(protein, 2, protein.Length, 0, CleavageSpecificity.Full, "full:M cleaved"));
                }

                // Also digest using the proteolysis product start/end indices
                peptides.AddRange(
                    protein.ProteolysisProducts
                    .Where(proteolysisProduct => proteolysisProduct.OneBasedEndPosition.HasValue && proteolysisProduct.OneBasedBeginPosition.HasValue
                        && OkayLength(proteolysisProduct.OneBasedEndPosition.Value - proteolysisProduct.OneBasedBeginPosition.Value + 1, minPeptidesLength, maxPeptidesLength))
                    .Select(proteolysisProduct =>
                        new ProteolyticPeptide(protein, proteolysisProduct.OneBasedBeginPosition.Value, proteolysisProduct.OneBasedEndPosition.Value, 0, CleavageSpecificity.Full, proteolysisProduct.Type)));
            }

            // Full proteolytic cleavage
            else if (CleavageSpecificity == CleavageSpecificity.Full)
            {
                peptides.AddRange(FullDigestion(protein, initiatorMethionineBehavior, maximumMissedCleavages, minPeptidesLength, maxPeptidesLength));
            }

            // Cleavage rules for semi-specific search
            else if (CleavageSpecificity == CleavageSpecificity.Semi)
            {
                peptides.AddRange(SemiProteolyticDigestion(protein, initiatorMethionineBehavior, maximumMissedCleavages, minPeptidesLength, maxPeptidesLength));
            }
            else
            {
                throw new NotImplementedException();
            }

            return peptides;
        }

        /// <summary>
        /// Gets the indices after which this protease will cleave a given protein sequence
        /// </summary>
        /// <param name="proteinSequence"></param>
        /// <returns></returns>
        internal List<int> GetDigestionSiteIndices(string proteinSequence)
        {
            var indices = new List<int>();
            for (int i = 0; i < proteinSequence.Length - 1; i++)
            {
                foreach (var c in SequencesInducingCleavage)
                {
                    if (SequenceInducesCleavage(proteinSequence, i, c)
                        && !SequencesPreventingCleavage.Any(nc => SequencePreventsCleavage(proteinSequence, i, nc)))
                    {
                        indices.Add(i + 1);
                        break;
                    }
                }
            }
            indices.Insert(0, 0); // The start of the protein is treated as a cleavage site to retain the n-terminal peptide
            indices.Add(proteinSequence.Length); // The end of the protein is treated as a cleavage site to retain the c-terminal peptide
            return indices;
        }

        /// <summary>
        /// Retain N-terminal residue?
        /// </summary>
        /// <param name="oneBasedCleaveAfter"></param>
        /// <param name="initiatorMethionineBehavior"></param>
        /// <param name="nTerminus"></param>
        /// <returns></returns>
        internal static bool Retain(int oneBasedCleaveAfter, InitiatorMethionineBehavior initiatorMethionineBehavior, char nTerminus)
        {
            return oneBasedCleaveAfter != 0 // this only pertains to the n-terminus
                || initiatorMethionineBehavior != InitiatorMethionineBehavior.Cleave
                || nTerminus != 'M';
        }

        /// <summary>
        /// Cleave N-terminal residue?
        /// </summary>
        /// <param name="oneBasedCleaveAfter"></param>
        /// <param name="initiatorMethionineBehavior"></param>
        /// <param name="nTerminus"></param>
        /// <returns></returns>
        internal static bool Cleave(int oneBasedCleaveAfter, InitiatorMethionineBehavior initiatorMethionineBehavior, char nTerminus)
        {
            return oneBasedCleaveAfter == 0 // this only pertains to the n-terminus
                && initiatorMethionineBehavior != InitiatorMethionineBehavior.Retain
                && nTerminus == 'M';
        }

        /// <summary>
        /// Is length of given peptide okay, given minimum and maximum?
        /// </summary>
        /// <param name="peptideLength"></param>
        /// <param name="minPeptidesLength"></param>
        /// <param name="maxPeptidesLength"></param>
        /// <returns></returns>
        internal static bool OkayLength(int? peptideLength, int? minPeptidesLength, int? maxPeptidesLength)
        {
            return OkayMinLength(peptideLength, minPeptidesLength) && OkayMaxLength(peptideLength, maxPeptidesLength);
        }

        /// <summary>
        /// Gets protein intervals for digestion by this specific protease.
        /// </summary>
        /// <param name="protein"></param>
        /// <param name="initiatorMethionineBehavior"></param>
        /// <param name="maximumMissedCleavages"></param>
        /// <param name="minPeptidesLength"></param>
        /// <param name="maxPeptidesLength"></param>
        /// <returns></returns>
        private IEnumerable<ProteolyticPeptide> FullDigestion(Protein protein, InitiatorMethionineBehavior initiatorMethionineBehavior,
            int maximumMissedCleavages, int minPeptidesLength, int maxPeptidesLength)
        {
            List<int> oneBasedIndicesToCleaveAfter = GetDigestionSiteIndices(protein.BaseSequence);
            for (int missedCleavages = 0; missedCleavages <= maximumMissedCleavages; missedCleavages++)
            {
                for (int i = 0; i < oneBasedIndicesToCleaveAfter.Count - missedCleavages - 1; i++)
                {
                    if (Retain(i, initiatorMethionineBehavior, protein[0])
                        && OkayLength(oneBasedIndicesToCleaveAfter[i + missedCleavages + 1] - oneBasedIndicesToCleaveAfter[i], minPeptidesLength, maxPeptidesLength))
                    {
                        yield return new ProteolyticPeptide(protein, oneBasedIndicesToCleaveAfter[i] + 1, oneBasedIndicesToCleaveAfter[i + missedCleavages + 1],
                            missedCleavages, CleavageSpecificity.Full, "full");
                    }
                    if (Cleave(i, initiatorMethionineBehavior, protein[0])
                        && OkayLength(oneBasedIndicesToCleaveAfter[i + missedCleavages + 1] - 1, minPeptidesLength, maxPeptidesLength))
                    {
                        yield return new ProteolyticPeptide(protein, 2, oneBasedIndicesToCleaveAfter[i + missedCleavages + 1],
                            missedCleavages, CleavageSpecificity.Full, "full:M cleaved");
                    }
                }

                // Also digest using the proteolysis product start/end indices
                foreach (var proteolysisProduct in protein.ProteolysisProducts)
                {
                    if (proteolysisProduct.OneBasedBeginPosition != 1 || proteolysisProduct.OneBasedEndPosition != protein.Length)
                    {
                        int i = 0;
                        while (oneBasedIndicesToCleaveAfter[i] < proteolysisProduct.OneBasedBeginPosition)
                        {
                            i++;
                        }

                        bool startPeptide = i + missedCleavages < oneBasedIndicesToCleaveAfter.Count
                            && oneBasedIndicesToCleaveAfter[i + missedCleavages] <= proteolysisProduct.OneBasedEndPosition
                            && proteolysisProduct.OneBasedBeginPosition.HasValue
                            && OkayLength(oneBasedIndicesToCleaveAfter[i + missedCleavages] - proteolysisProduct.OneBasedBeginPosition.Value + 1, minPeptidesLength, maxPeptidesLength);
                        if (startPeptide)
                        {
                            yield return new ProteolyticPeptide(protein, proteolysisProduct.OneBasedBeginPosition.Value, oneBasedIndicesToCleaveAfter[i + missedCleavages],
                                missedCleavages, CleavageSpecificity.Full, proteolysisProduct.Type + " start");
                        }

                        while (oneBasedIndicesToCleaveAfter[i] < proteolysisProduct.OneBasedEndPosition)
                        {
                            i++;
                        }

                        bool end = i - missedCleavages - 1 >= 0
                            && oneBasedIndicesToCleaveAfter[i - missedCleavages - 1] + 1 >= proteolysisProduct.OneBasedBeginPosition
                            && proteolysisProduct.OneBasedEndPosition.HasValue
                            && OkayLength(proteolysisProduct.OneBasedEndPosition.Value - oneBasedIndicesToCleaveAfter[i - missedCleavages - 1] + 1 - 1, minPeptidesLength, maxPeptidesLength);
                        if (end)
                        {
                            yield return new ProteolyticPeptide(protein, oneBasedIndicesToCleaveAfter[i - missedCleavages - 1] + 1, proteolysisProduct.OneBasedEndPosition.Value,
                                missedCleavages, CleavageSpecificity.Full, proteolysisProduct.Type + " end");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the protein intervals based on semiSpecific digestion rules
        /// This is the classic semi-specific digestion
        /// </summary>
        /// <param name="protein"></param>
        /// <param name="initiatorMethionineBehavior"></param>
        /// <param name="maximumMissedCleavages"></param>
        /// <param name="minPeptidesLength"></param>
        /// <param name="maxPeptidesLength"></param>
        /// <returns></returns>
        private IEnumerable<ProteolyticPeptide> SemiProteolyticDigestion(Protein protein, InitiatorMethionineBehavior initiatorMethionineBehavior,
            int maximumMissedCleavages, int minPeptidesLength, int maxPeptidesLength)
        {
            List<ProteolyticPeptide> intervals = new List<ProteolyticPeptide>();
            List<int> oneBasedIndicesToCleaveAfter = GetDigestionSiteIndices(protein.BaseSequence);
            for (int i = 0; i < oneBasedIndicesToCleaveAfter.Count - maximumMissedCleavages - 1; i++) //it's possible not to go through this loop, and that's okay. It will get caught in the next loop
            {
                bool retain = Retain(i, initiatorMethionineBehavior, protein[0]);
                bool cleave = Cleave(i, initiatorMethionineBehavior, protein[0]);
                int cTerminusProtein = oneBasedIndicesToCleaveAfter[i + maximumMissedCleavages + 1];
                HashSet<int> localOneBasedIndicesToCleaveAfter = new HashSet<int>();
                for (int j = i; j < i + maximumMissedCleavages + 1; j++)
                {
                    localOneBasedIndicesToCleaveAfter.Add(oneBasedIndicesToCleaveAfter[j]);
                }
                if (retain)
                {
                    intervals.AddRange(FixedTermini(oneBasedIndicesToCleaveAfter[i], cTerminusProtein, protein, cleave, minPeptidesLength, maxPeptidesLength, localOneBasedIndicesToCleaveAfter));
                }

                if (cleave)
                {
                    intervals.AddRange(FixedTermini(1, cTerminusProtein, protein, cleave, minPeptidesLength, maxPeptidesLength, localOneBasedIndicesToCleaveAfter));
                }
            }

            //finish C-term of protein caused by loop being "i < oneBasedIndicesToCleaveAfter.Count - maximumMissedCleavages - 1"
            int last = oneBasedIndicesToCleaveAfter.Count - 1;
            int maxIndexSemi = maximumMissedCleavages < last ? maximumMissedCleavages : last;
            //Fringe C-term peptides
            for (int i = 1; i <= maxIndexSemi; i++)
            {
                //fixedN
                int nTerminusProtein = oneBasedIndicesToCleaveAfter[last - i];
                int cTerminusProtein = oneBasedIndicesToCleaveAfter[last];
                HashSet<int> localOneBasedIndicesToCleaveAfter = new HashSet<int>();
                for (int j = 1; j < i; j++) //j starts at 1, because zero is c terminus
                {
                    localOneBasedIndicesToCleaveAfter.Add(oneBasedIndicesToCleaveAfter[last - j]);
                }
                for (int j = cTerminusProtein - 1; j > nTerminusProtein; j--)//minus 1 so as to not double count the c terminus. If the c-terminus was not hit earlier, it will be hit in fringe n-term
                {
                    if (OkayLength(j - nTerminusProtein, minPeptidesLength, maxPeptidesLength))
                    {
                        intervals.Add(localOneBasedIndicesToCleaveAfter.Contains(j) ?
                            new ProteolyticPeptide(protein, nTerminusProtein + 1, j, j - nTerminusProtein, CleavageSpecificity.Full, "full") :
                            new ProteolyticPeptide(protein, nTerminusProtein + 1, j, j - nTerminusProtein, CleavageSpecificity.Semi, "semi"));
                    }
                }
            }

            //Fringe N-term peptides
            for (int i = 1; i <= maxIndexSemi; i++)
            {
                //fixedC
                int nTerminusProtein = oneBasedIndicesToCleaveAfter[0] + 1;
                int cTerminusProtein = oneBasedIndicesToCleaveAfter[i];
                HashSet<int> localOneBasedIndicesToCleaveAfter = new HashSet<int>();
                for (int j = 1; j < i; j++)//j starts at 1, because zero is n terminus
                {
                    localOneBasedIndicesToCleaveAfter.Add(oneBasedIndicesToCleaveAfter[j]);
                }
                for (int j = nTerminusProtein + 1; j < cTerminusProtein; j++) //plus one to not doublecount the n terminus
                {
                    if (OkayLength(cTerminusProtein - j, minPeptidesLength, maxPeptidesLength))
                    {
                        intervals.Add(localOneBasedIndicesToCleaveAfter.Contains(j) ?
                            new ProteolyticPeptide(protein, j + 1, cTerminusProtein, cTerminusProtein - j, CleavageSpecificity.Full, "full") :
                            new ProteolyticPeptide(protein, j + 1, cTerminusProtein, cTerminusProtein - j, CleavageSpecificity.Semi, "semi"));
                    }
                }
            }

            // Also digest using the proteolysis product start/end indices
            // This should only be things where the proteolysis is not K/R and the
            foreach (var proteolysisProduct in protein.ProteolysisProducts)
            {
                if (proteolysisProduct.OneBasedEndPosition.HasValue && proteolysisProduct.OneBasedBeginPosition.HasValue
                    && (proteolysisProduct.OneBasedBeginPosition != 1 || proteolysisProduct.OneBasedEndPosition != protein.Length)) //if at least one side is not a terminus
                {
                    int i = 0;
                    while (oneBasedIndicesToCleaveAfter[i] < proteolysisProduct.OneBasedBeginPosition)//"<" to prevent additions if same index as residues
                    {
                        i++; //last position in protein is an index to cleave after
                    }

                    // Start peptide
                    for (int j = proteolysisProduct.OneBasedBeginPosition.Value; j < oneBasedIndicesToCleaveAfter[i]; j++)
                    {
                        if (OkayLength(j - proteolysisProduct.OneBasedBeginPosition + 1, minPeptidesLength, maxPeptidesLength))
                        {
                            intervals.Add(new ProteolyticPeptide(protein, proteolysisProduct.OneBasedBeginPosition.Value, j,
                                j - proteolysisProduct.OneBasedBeginPosition.Value, CleavageSpecificity.Full, proteolysisProduct.Type + " start"));
                        }
                    }
                    while (oneBasedIndicesToCleaveAfter[i] < proteolysisProduct.OneBasedEndPosition) //"<" to prevent additions if same index as residues, since i-- is below
                    {
                        i++;
                    }

                    //Now that we've obtained an index to cleave after that is past the proteolysis product
                    //we need to backtrack to get the index to cleave that is immediately before the the proteolysis product
                    //to do this, we will do i--
                    //In the nitch case that the proteolysis product is already an index to cleave
                    //no new peptides will be generated using this, so we will forgo i--
                    //this makes peptides of length 0, which are not generated due to the for loop
                    //removing this if statement will result in crashes from c-terminal proteolysis product end positions
                    if (oneBasedIndicesToCleaveAfter[i] != proteolysisProduct.OneBasedEndPosition)
                    {
                        i--;
                    }

                    // End
                    for (int j = oneBasedIndicesToCleaveAfter[i] + 1; j < proteolysisProduct.OneBasedEndPosition.Value; j++)
                    {
                        if (OkayLength(proteolysisProduct.OneBasedEndPosition - j + 1, minPeptidesLength, maxPeptidesLength))
                        {
                            intervals.Add(new ProteolyticPeptide(protein, j, proteolysisProduct.OneBasedEndPosition.Value,
                                proteolysisProduct.OneBasedEndPosition.Value - j, CleavageSpecificity.Full, proteolysisProduct.Type + " end"));
                        }
                    }
                }
            }
            return intervals;
        }

        /// <summary>
        /// Get protein intervals for fixed termini. For classic semi-proteolytic cleavage.
        /// </summary>
        /// <param name="nTerminusProtein"></param>
        /// <param name="cTerminusProtein"></param>
        /// <param name="protein"></param>
        /// <param name="cleave"></param>
        /// <param name="minPeptidesLength"></param>
        /// <param name="maxPeptidesLength"></param>
        /// <returns></returns>
        private static IEnumerable<ProteolyticPeptide> FixedTermini(int nTerminusProtein, int cTerminusProtein, Protein protein, bool cleave, int minPeptidesLength, int maxPeptidesLength, HashSet<int> localOneBasedIndicesToCleaveAfter)
        {
            List<ProteolyticPeptide> intervals = new List<ProteolyticPeptide>();
            if (OkayLength(cTerminusProtein - nTerminusProtein, minPeptidesLength, maxPeptidesLength)) //adds the full length maximum cleavages, no semi
            {
                intervals.Add(new ProteolyticPeptide(protein, nTerminusProtein + 1, cTerminusProtein,
                    cTerminusProtein - nTerminusProtein, CleavageSpecificity.Full, "full" + (cleave ? ":M cleaved" : ""))); // Maximum sequence length
            }

            // Fixed termini at each internal index
            IEnumerable<int> internalIndices = Enumerable.Range(nTerminusProtein + 1, cTerminusProtein - nTerminusProtein - 1); //every residue between them, +1 so we don't double count the original full
            IEnumerable<ProteolyticPeptide> fixedCTermIntervals =
                internalIndices
                .Where(j => OkayLength(cTerminusProtein - j, minPeptidesLength, maxPeptidesLength))
                .Select(j => localOneBasedIndicesToCleaveAfter.Contains(j) ?
                new ProteolyticPeptide(protein, j + 1, cTerminusProtein, cTerminusProtein - j, CleavageSpecificity.Full, "full" + (cleave ? ":M cleaved" : "")) :
                new ProteolyticPeptide(protein, j + 1, cTerminusProtein, cTerminusProtein - j, CleavageSpecificity.Semi, "semi" + (cleave ? ":M cleaved" : "")));
            IEnumerable<ProteolyticPeptide> fixedNTermIntervals = //don't allow full for these, since otherwise we will double count
                internalIndices
                .Where(j => OkayLength(j - nTerminusProtein, minPeptidesLength, maxPeptidesLength))
                .Select(j => localOneBasedIndicesToCleaveAfter.Contains(j) ?
                null : //new ProteolyticPeptide(protein, nTerminusProtein + 1, j, j - nTerminusProtein, CleavageSpecificity.Full, "full" + (cleave ? ":M cleaved" : "")) : //don't allow full, since they're covered by Cterm
                new ProteolyticPeptide(protein, nTerminusProtein + 1, j, j - nTerminusProtein, CleavageSpecificity.Semi, "semi" + (cleave ? ":M cleaved" : "")))
                .Where(j => j != null);

            return intervals.Concat(fixedCTermIntervals).Concat(fixedNTermIntervals);
        }

        /// <summary>
        /// Checks if select subsequence of protein matches sequence that induces cleavage
        /// </summary>
        /// <param name="proteinSequence"></param>
        /// <param name="proteinSequenceIndex"></param>
        /// <param name="sequenceInducingCleavage"></param>
        /// <returns></returns>
        private bool SequenceInducesCleavage(string proteinSequence, int proteinSequenceIndex, Tuple<string, FragmentationTerminus> sequenceInducingCleavage)
        {
            return (sequenceInducingCleavage.Item2 != FragmentationTerminus.N
                    && proteinSequenceIndex - sequenceInducingCleavage.Item1.Length + 1 >= 0
                    && proteinSequence.Substring(proteinSequenceIndex - sequenceInducingCleavage.Item1.Length + 1, sequenceInducingCleavage.Item1.Length)
                        .Equals(sequenceInducingCleavage.Item1, StringComparison.OrdinalIgnoreCase))
                || (sequenceInducingCleavage.Item2 == FragmentationTerminus.N
                    && proteinSequenceIndex + 1 + sequenceInducingCleavage.Item1.Length <= proteinSequence.Length
                    && proteinSequence.Substring(proteinSequenceIndex + 1, sequenceInducingCleavage.Item1.Length)
                        .Equals(sequenceInducingCleavage.Item1, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Checks if select subsequence of protein matches sequence preventing cleavage
        /// </summary>
        /// <param name="proteinSequence"></param>
        /// <param name="proteinSequenceIndex"></param>
        /// <param name="sequencePreventingCleavage"></param>
        /// <returns></returns>
        private bool SequencePreventsCleavage(string proteinSequence, int proteinSequenceIndex, Tuple<string, FragmentationTerminus> sequencePreventingCleavage)
        {
            return (sequencePreventingCleavage.Item2 != FragmentationTerminus.N
                    && proteinSequenceIndex + 1 + sequencePreventingCleavage.Item1.Length <= proteinSequence.Length
                    && proteinSequence.Substring(proteinSequenceIndex + 1, sequencePreventingCleavage.Item1.Length)
                        .Equals(sequencePreventingCleavage.Item1, StringComparison.OrdinalIgnoreCase))
                || (SequencesInducingCleavage.First().Item2 == FragmentationTerminus.N
                    && proteinSequenceIndex - sequencePreventingCleavage.Item1.Length + 1 >= 0
                    && proteinSequence.Substring(proteinSequenceIndex - sequencePreventingCleavage.Item1.Length + 1, sequencePreventingCleavage.Item1.Length)
                        .Equals(sequencePreventingCleavage.Item1, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Is length of given peptide okay, given minimum?
        /// </summary>
        /// <param name="peptideLength"></param>
        /// <param name="minPeptidesLength"></param>
        /// <returns></returns>
        private static bool OkayMinLength(int? peptideLength, int? minPeptidesLength)
        {
            return !minPeptidesLength.HasValue || peptideLength >= minPeptidesLength;
        }

        /// <summary>
        /// Is length of given peptide okay, given maximum?
        /// </summary>
        /// <param name="peptideLength"></param>
        /// <param name="maxPeptidesLength"></param>
        /// <returns></returns>
        private static bool OkayMaxLength(int? peptideLength, int? maxPeptidesLength)
        {
            return !maxPeptidesLength.HasValue || peptideLength <= maxPeptidesLength;
        }
    }
}