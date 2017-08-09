//-----------------------------------------------------------------------
// <copyright file="ProjectModel.cs" company="NuGet Reference Switcher">
//     Copyright (c) Rico Suter. All rights reserved.
// </copyright>
// <license>http://nugetreferenceswitcher.codeplex.com/license</license>
// <author>Rico Suter, mail@rsuter.com</author>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Windows.Threading;
using EnvDTE;
using MyToolkit.Collections;
using VSLangProj;

namespace NuGetReferenceSwitcher.Presentation.Models
{
    public class ProjectModel
    {
        private readonly VSProject _vsProject;

        /// <summary>Initializes a new instance of the <see cref="ProjectModel"/> class. </summary>
        /// <param name="project">The native project object. </param>
        public ProjectModel(VSProject project)
        {
            _vsProject = project;
            
            Name = project.Project.Name;
            LoadReferences();
        }

        /// <summary>Gets the name of the project. </summary>
        public string Name { get; private set; }

        /// <summary>Gets the project's references. </summary>
        public ExtendedObservableCollection<ReferenceModel> References { get; private set; }

        /// <summary>Gets the project's NuGet references (subset of References). </summary>
        public ExtendedObservableCollection<ReferenceModel> NuGetReferences { get; private set; }

        /// <summary>Gets the path of the current configuration. </summary>
        public string CurrentConfigurationPath
        {
            get { return GetConfigurationPath(".nugetreferenceswitcher"); }
        }

        /// <summary>Gets the path of the previous configuration. </summary>
        public string PreviousConfigurationPath
        {
            get { return GetConfigurationPath(".previous.nugetreferenceswitcher"); }
        }
        
        /// <summary>Gets the current project reference to NuGet reference transformations. </summary>
        public List<FromProjectToNuGetTransformation> CurrentToNuGetTransformations
        {
            get { return LoadTransformationsFromFile(CurrentConfigurationPath); }
        }

        /// <summary>Gets the previous project reference to NuGet reference transformations. </summary>
        public List<FromProjectToNuGetTransformation> PreviousToNuGetTransformations
        {
            get { return LoadTransformationsFromFile(PreviousConfigurationPath); }
        }

        /// <summary>Gets the project file path. </summary>
        public string Path
        {
            get { return _vsProject.Project.FileName; }
        }

        /// <summary>Deletes the previous configuration file and renames the current 
        /// configuration file to the path of the previous configuration file.  </summary>
        public void DeleteConfigurationFile()
        {               
            if (File.Exists(CurrentConfigurationPath))
            {
                if (File.Exists(PreviousConfigurationPath))
                    File.Delete(PreviousConfigurationPath);

                File.Move(CurrentConfigurationPath, PreviousConfigurationPath);
            }
        }

        /// <summary>Adds a project reference to the project. </summary>
        /// <param name="project">The project to add. </param>
        public void AddProjectReference(ProjectModel project)
        {
            _vsProject.References.AddProject(project._vsProject.Project);
        }

        public void RemoveReference(ReferenceModel reference)
        {
        
        }

        /// <summary>Saves the project. </summary>
        public void Save()
        {
            // TODO: This may lock up the UI => check and fix
            Dispatcher.CurrentDispatcher.InvokeAsync(() =>
            {
                _vsProject.Project.Save();
            });
        }

        /// <summary>Adds an assembly reference to the project.</summary>
        /// <param name="assemblyPath">The assembly path.</param>
        /// <returns><c>true</c> when the file could be added.</returns>
        public bool AddReference(string assemblyPath)
        {

            foreach (Reference reference in _vsProject.References)
            {
                // skip out early if there is still a reference
                if (reference.Path.Equals(assemblyPath))
                    return true;
            }
            if (File.Exists(assemblyPath))
            {
                _vsProject.References.Add(assemblyPath);
                return true;
            }
            return false; 
        }

        /// <summary>Removes the project from the solution. </summary>
        /// <param name="solution">The solution to remove the project from. </param>
        public void RemoveFromSolution(Solution solution)
        {
            try
            {
                _vsProject.Refresh();
            }
            catch (Exception)
            {
                
            }
            solution.Remove(_vsProject.Project);
        }

        private string GetConfigurationPath(string fileExtension)
        {
            var projectPath = _vsProject.Project.FileName;
            var projectDirectory = System.IO.Path.GetDirectoryName(projectPath);
            var projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath);
            return System.IO.Path.Combine(projectDirectory, projectName + fileExtension);
        }

        private List<FromProjectToNuGetTransformation> LoadTransformationsFromFile(string configurationPath)
        {
            var list = new List<FromProjectToNuGetTransformation>();
            if (File.Exists(configurationPath))
            {
                var lines = File.ReadAllLines(configurationPath)
                    .Select(l => l.Split('\t'))
                    .Where(l => l.Length == 4).ToArray();

                foreach (var line in lines)
                    list.Add(new FromProjectToNuGetTransformation {
                        FromProjectName = line[0],
                        FromProjectPath = PathUtilities.MakeAbsolute(line[1], configurationPath),
                        ToAssemblyPath = PathUtilities.MakeAbsolute(line[2], configurationPath),
                        Removed = bool.Parse(line[3])
                    });
            }
            return list;
        }

        private void LoadReferences()
        {
            References = new ExtendedObservableCollection<ReferenceModel>();
            NuGetReferences = new ExtendedObservableCollection<ReferenceModel>();

            foreach (var vsReference in _vsProject.References.OfType<Reference>())
            {
                var reference = new ReferenceModel(vsReference);
                References.Add(reference);
                if (vsReference.Path.Contains("/packages/") || vsReference.Path.Contains("\\packages\\"))
                    NuGetReferences.Add(reference);
            }
        }
    }
}