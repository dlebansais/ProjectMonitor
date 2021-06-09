namespace Monitor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    public partial class ProjectValidation
    {
        private void ValidatePackageReferenceVersions()
        {
            Dictionary<string, List<string>> PackageReferenceTable = new();

            foreach (ProjectInfo Project in GitProbe.ProjectList)
            {
                foreach (SlnExplorer.PackageReference PackageReference in Project.PackageReferenceList)
                {
                    string Name = PackageReference.Name;
                    string Version = PackageReference.Version;

                    if (!PackageReferenceTable.ContainsKey(Name))
                        PackageReferenceTable.Add(Name, new List<string>());

                    List<string> ReferenceList = PackageReferenceTable[Name];
                    if (!ReferenceList.Contains(Version))
                        ReferenceList.Add(Version);
                }
            }

            foreach (KeyValuePair<string, List<string>> Entry in PackageReferenceTable)
            {
                List<string> ReferenceList = Entry.Value;
                ReferenceList.Sort();
            }

            foreach (KeyValuePair<string, List<string>> Entry in PackageReferenceTable)
            {
                string Name = Entry.Key;
                List<string> ReferenceList = Entry.Value;

                if (ReferenceList.Count > 1)
                {
                    string MinVersion = ReferenceList[0];
                    string MaxVersion = ReferenceList[ReferenceList.Count - 1];

                    string ErrorText = $"Package {Name} referenced with several versions from {MinVersion} to {MaxVersion}";
                    ErrorList.Add(new PackageError(ErrorText));
                    InvalidateProjectsWithOldVersion(Name, MaxVersion);
                }
            }
        }

        private void InvalidateProjectsWithOldVersion(string name, string maxVersion)
        {
            foreach (ProjectInfo Project in GitProbe.ProjectList)
                InvalidateProjectsWithOldVersion(Project, name, maxVersion);
        }

        private void InvalidateProjectsWithOldVersion(ProjectInfo project, string name, string maxVersion)
        {
            foreach (SlnExplorer.PackageReference PackageReference in project.PackageReferenceList)
                if (PackageReference.Name == name && PackageReference.Version != maxVersion)
                {
                    project.Invalidate();
                    break;
                }
        }

        public void ValidatePackageReferenceConditions(List<string> shortNameList)
        {
            foreach (ProjectInfo Project in GitProbe.ProjectList)
                ValidatePackageReferenceConditions(Project, shortNameList);
        }

        public void ValidatePackageReferenceConditions(ProjectInfo project, List<string> shortNameList)
        {
            List<string> ValidPackageList = new();

            foreach (string ShortName in shortNameList)
                ValidatePackageDebugReleaseConditions(project, ShortName, ValidPackageList);

            foreach (string ShortName in ValidPackageList)
                ValidatePackageValidConditions(project, ShortName);
        }

        private void ValidatePackageValidConditions(ProjectInfo project, string shortName)
        {
            string ShortNameDebug = $"{shortName}-Debug";

            foreach (SlnExplorer.PackageReference PackageReference in project.PackageReferenceList)
            {
                string Name = PackageReference.Name;
                string Condition = PackageReference.Condition;

                if ((Name == shortName && Condition != "'$(Configuration)|$(Platform)'!='Debug|x64'") || (Name == ShortNameDebug && Condition != "'$(Configuration)|$(Platform)'=='Debug|x64'"))
                {
                    string ErrorText = $"Project {project.ProjectName} use package {Name} but with wrong condition {Condition}";
                    AddErrorIfNewOnly(project.ParentSolution.ParentRepository, ErrorText);
                    project.Invalidate();
                }
            }
        }

        private void ValidatePackageDebugReleaseConditions(ProjectInfo project, string shortName, List<string> validPackageList)
        {
            bool HasMainPackage = false;
            bool HasDebugPackage = false;

            foreach (SlnExplorer.PackageReference PackageReference in project.PackageReferenceList)
            {
                if (PackageReference.Name == shortName)
                    HasMainPackage = true;
                if (PackageReference.Name == $"{shortName}-Debug")
                    HasDebugPackage = true;
            }

            if (HasMainPackage && HasDebugPackage)
                validPackageList.Add(shortName);
            else if (HasMainPackage || HasDebugPackage)
            {
                string ErrorText;

                if (HasMainPackage)
                    ErrorText = $"Project {project.ProjectName} has package {shortName}-Debug but no release version";
                else
                    ErrorText = $"Project {project.ProjectName} has package {shortName} but no debug version";

                AddErrorIfNewOnly(project.ParentSolution.ParentRepository, ErrorText);
                project.Invalidate();
            }
        }

        public List<string> GetPackageShortNameList()
        {
            List<string> ShortNameList = new();

            foreach (ProjectInfo Project in GitProbe.ProjectList)
            {
                foreach (SlnExplorer.PackageReference PackageReference in Project.PackageReferenceList)
                {
                    string Name = PackageReference.Name;
                    if (!Name.EndsWith("-Debug"))
                        continue;

                    string ShortName = Name.Substring(0, Name.Length - 6);
                    ShortNameList.Add(ShortName);
                }
            }

            return ShortNameList;
        }
    }
}
