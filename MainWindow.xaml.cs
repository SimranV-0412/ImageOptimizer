﻿//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Windows;
//using System.Windows.Forms;
//using System.Windows.Threading;
//using ImageMagick;

//namespace Image_optimizer
//{
//    public partial class MainWindow : Window
//    {
//        private long totalImages;
//        private long totalSizeReduction;
//        private DispatcherTimer loadingTimer;
//        private int ImagesPerBatch;
//        private CancellationTokenSource cancellationTokenSource; // Used to cancel the optimization process
//        private List<string> processedImageFiles; // To keep track of processed image files
//        private double pausedProgress; // To store the progress when pausing

//        public MainWindow()
//        {
//            InitializeComponent();
//            processedImageFiles = new List<string>();
//        }

//        private void BrowseButton_Click(object sender, RoutedEventArgs e)
//        {
//            using (var dialog = new FolderBrowserDialog())
//            {
//                DialogResult result = dialog.ShowDialog();
//                if (result == System.Windows.Forms.DialogResult.OK)
//                {
//                    string selectedDirectory = dialog.SelectedPath;
//                    SelectedDirectoryTextBox.Text = selectedDirectory;
//                }
//            }
//        }
//        private async void ButtonOptimize_ClickAsync(object sender, RoutedEventArgs e)
//        {
//            string directoryPath = SelectedDirectoryTextBox.Text;
//            string[] directories = Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories);

//            totalImages = directories.Sum(directory => Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
//                .Count(file => file.ToLower().EndsWith(".jpg") || file.ToLower().EndsWith(".jpeg") || file.ToLower().EndsWith(".png")));

//            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
//            {
//                TotalImagesLabel.Content = $"Total Images: {totalImages}";
//                TotalImagesLabel.Visibility = Visibility.Visible;
//                ProgressBar.Visibility = Visibility.Visible;
//                ProgressLabel.Visibility = Visibility.Visible;
//                ImageNameLabel.Visibility = Visibility.Visible;
//                ResumeButton.Visibility = Visibility.Visible;
//                PauseButton.Visibility = Visibility.Visible;
//                ImageListTextBox.Visibility = Visibility.Hidden;
//            });

//            int completedImages = 0;
//            cancellationTokenSource = new CancellationTokenSource(); // Create a new CancellationTokenSource

//            const int batchSize = 100; // Set the batch size as per your system's capability

//            ConcurrentBag<string> imageFiles = new ConcurrentBag<string>();

//            foreach (string directory in directories)
//            {
//                string[] files = Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
//                    .Where(file => file.ToLower().EndsWith(".jpg") || file.ToLower().EndsWith(".jpeg") || file.ToLower().EndsWith(".png"))
//                    .ToArray();

//                foreach (string file in files)
//                {
//                    imageFiles.Add(file);
//                }
//            }

//            int processedCount = 0;
//            int imagesInCurrentBatch = 0;
//            string currentBatchImageNames = string.Empty;

//            var tasks = new List<Task>();

//            while (imageFiles.TryTake(out string imageFile))
//            {
//                // Check if the image file has already been processed
//                if (processedImageFiles.Contains(imageFile))
//                    continue;

//                tasks.Add(Task.Run(async () =>
//                {
//                    try
//                    {
//                        // Check if cancellation was requested
//                        cancellationTokenSource.Token.ThrowIfCancellationRequested();

//                        FileInfo fileInfo = new FileInfo(imageFile);
//                        long originalSize = fileInfo.Length;

//                        using (var image = new MagickImage(imageFile))
//                        {
//                            // Check if the image format is valid
//                            if (image.Format != MagickFormat.Unknown)
//                            {
//                                // Perform optimization
//                                image.Strip();
//                                image.ColorSpace = ColorSpace.RGB;

//                                await image.WriteAsync(imageFile);

//                                long optimizedSize = new FileInfo(imageFile).Length;

//                                long sizeReduction = originalSize - optimizedSize;
//                                totalSizeReduction += sizeReduction;

//                                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
//                                {
//                                    Interlocked.Increment(ref completedImages); // Increment completedImages safely

//                                    double reductionPercentage = (sizeReduction / (double)originalSize) * 100;

//                                    // Calculate the progress based on the number of completed images and the total number of images
//                                    double progress = (completedImages / (double)totalImages) * 100;

//                                    ProgressBar.Value = progress;
//                                    ProgressLabel.Content = $"Progress: {progress:F2}% | Size Reduction: {reductionPercentage:F2}%";
//                                    ImageNameLabel.Text = $"Image: {System.IO.Path.GetFileName(imageFile)}";

//                                    // Add current image name to the batch string
//                                    currentBatchImageNames += $"{System.IO.Path.GetFileName(imageFile)}\n";
//                                    imagesInCurrentBatch++;

//                                    // If the batch size is reached, append the batch string to the TextBox
//                                    if (imagesInCurrentBatch == ImagesPerBatch)
//                                    {
//                                        ImageListTextBox.AppendText(currentBatchImageNames);
//                                        ImageListTextBox.ScrollToEnd();

//                                        // Reset batch variables
//                                        currentBatchImageNames = string.Empty;
//                                        imagesInCurrentBatch = 0;
//                                    }
//                                });
//                            }
//                            else
//                            {
//                                System.Windows.MessageBox.Show($"Invalid image format: {imageFile}");
//                            }
//                        }

//                        // Add the processed image file to the list
//                        lock (processedImageFiles)
//                        {
//                            processedImageFiles.Add(imageFile);
//                        }

//                        int currentCompleted = System.Threading.Interlocked.Increment(ref completedImages);
//                        processedCount++;

//                        if (processedCount % batchSize == 0)
//                        {
//                            await Task.Delay(1); // Allow other tasks to execute
//                        }
//                    }
//                    catch (OperationCanceledException)
//                    {
//                        // Optimization process was canceled
//                        throw;
//                    }
//                }, cancellationTokenSource.Token)); // Pass the CancellationToken to the Task.Run method
//            }

//            // Wait for all tasks to complete or cancellation is requested
//            try
//            {
//                await Task.WhenAll(tasks);
//            }
//            catch (OperationCanceledException)
//            {
//                System.Windows.MessageBox.Show("Image optimization paused.");
//                return;
//            }

//            // Append any remaining images in the last batch to the TextBox
//            if (!string.IsNullOrEmpty(currentBatchImageNames))
//            {
//                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
//                {
//                    ImageListTextBox.AppendText(currentBatchImageNames);
//                    ImageListTextBox.ScrollToEnd();
//                });
//            }
//            ImageListTextBox.Visibility = Visibility.Visible;

//            System.Windows.MessageBox.Show($"Image optimization completed.\nTotal Size Reduction: {totalSizeReduction} bytes");

//            // Reset visibility of elements
//            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
//            {
//                TotalImagesLabel.Visibility = Visibility.Hidden;
//                ProgressBar.Visibility = Visibility.Hidden;
//                ProgressLabel.Visibility = Visibility.Hidden;
//                ImageNameLabel.Visibility = Visibility.Hidden;
//                ResumeButton.Visibility = Visibility.Hidden;
//                PauseButton.Visibility = Visibility.Hidden;
//                ImageListTextBox.Visibility = Visibility.Hidden;
//            });
//        }
//        private void ButtonPause_Click(object sender, RoutedEventArgs e)
//        {
//            // Store the current progress before pausing

//            // Cancel the optimization process
//            if (cancellationTokenSource != null)
//                cancellationTokenSource.Cancel();
//            pausedProgress = ProgressBar.Value;
//        }

//        private void ButtonResume_Click(object sender, RoutedEventArgs e)
//        {

//            // Cancel the current optimization process if it is active
//            if (cancellationTokenSource != null)
//                cancellationTokenSource.Cancel();

//            // Reset the completed images count
//            int completedImages = processedImageFiles.Count;

//            // Reset the total size reduction
//            totalSizeReduction = processedImageFiles.Sum(file => new FileInfo(file).Length);

//            // Start the optimization process again
//            cancellationTokenSource = new CancellationTokenSource();
//            ButtonOptimize_ClickAsync(sender, e);
//        }

//        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
//        {

//        }

//        private void SelectedDirectoryTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
//        {

//        }

//        private void ImageListTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
//        {

//        }

//        private void ClearButton_Click(object sender, RoutedEventArgs e)
//        {
//            SelectedDirectoryTextBox.Text = string.Empty;
//        }

//    }
//}









using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using ImageMagick;

namespace Image_optimizer
{
    public partial class MainWindow : Window
    {
        private long totalImages;
        private long totalSizeReduction;
        private DispatcherTimer loadingTimer;
        private int ImagesPerBatch;
        private CancellationTokenSource cancellationTokenSource; // Used to cancel the optimization process
        private List<string> processedImageFiles; // To keep track of processed image files
        private double pausedProgress; // To store the progress when pausing

        public MainWindow()
        {
            InitializeComponent();
            processedImageFiles = new List<string>();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    string selectedDirectory = dialog.SelectedPath;
                    SelectedDirectoryTextBox.Text = selectedDirectory;
                }
            }
        }

        private async void ButtonOptimize_ClickAsync(object sender, RoutedEventArgs e)
        {
            string directoryPath = SelectedDirectoryTextBox.Text;
            string[] directories = Directory.GetDirectories(directoryPath, "*", SearchOption.AllDirectories);

            totalImages = directories.Sum(directory => Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                .Count(file => file.ToLower().EndsWith(".jpg") || file.ToLower().EndsWith(".jpeg") || file.ToLower().EndsWith(".png")));

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TotalImagesLabel.Content = $"Total Images: {totalImages}";
                TotalImagesLabel.Visibility = Visibility.Visible;
                ProgressBar.Visibility = Visibility.Visible;
                ProgressLabel.Visibility = Visibility.Visible;
                ImageNameLabel.Visibility = Visibility.Visible;
                ResumeButton.Visibility = Visibility.Visible;
                PauseButton.Visibility = Visibility.Visible;
                ImageListTextBox.Visibility = Visibility.Hidden;
            });

            int completedImages = 0;
            cancellationTokenSource = new CancellationTokenSource(); // Create a new CancellationTokenSource

            const int batchSize = 100; // Set the batch size as per your system's capability

            ConcurrentBag<string> imageFiles = new ConcurrentBag<string>();

            foreach (string directory in directories)
            {
                string[] files = Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(file => file.ToLower().EndsWith(".jpg") || file.ToLower().EndsWith(".jpeg") || file.ToLower().EndsWith(".png"))
                    .ToArray();

                foreach (string file in files)
                {
                    imageFiles.Add(file);
                }
            }

            int processedCount = 0;
            int imagesInCurrentBatch = 0;
            string currentBatchImageNames = string.Empty;

            var tasks = new List<Task>();

            while (imageFiles.TryTake(out string imageFile))
            {
                // Check if the image file has already been processed
                if (processedImageFiles.Contains(imageFile))
                    continue;

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // Check if cancellation was requested
                        cancellationTokenSource.Token.ThrowIfCancellationRequested();

                        FileInfo fileInfo = new FileInfo(imageFile);
                        long originalSize = fileInfo.Length;

                        using (var image = new MagickImage(imageFile))
                        {
                            // Check if the image format is valid
                            if (image.Format != MagickFormat.Unknown)
                            {
                                // Perform optimization
                                image.Strip();
                                image.ColorSpace = ColorSpace.RGB;

                                await image.WriteAsync(imageFile);

                                long optimizedSize = new FileInfo(imageFile).Length;

                                long sizeReduction = originalSize - optimizedSize;
                                totalSizeReduction += sizeReduction;

                                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    Interlocked.Increment(ref completedImages); // Increment completedImages safely

                                    double reductionPercentage = (sizeReduction / (double)originalSize) * 100;

                                    // Calculate the progress based on the number of completed images and the total number of images
                                    double progress = (completedImages / (double)totalImages) * 100;

                                    ProgressBar.Value = progress;
                                    ProgressLabel.Content = $"Progress: {progress:F2}% | Size Reduction: {reductionPercentage:F2}%";
                                    ImageNameLabel.Text = $"Image: {System.IO.Path.GetFileName(imageFile)}";

                                    // Add current image name to the batch string
                                    currentBatchImageNames += $"{System.IO.Path.GetFileName(imageFile)}\n";
                                    imagesInCurrentBatch++;

                                    // If the batch size is reached, append the batch string to the TextBox
                                    if (imagesInCurrentBatch == ImagesPerBatch)
                                    {
                                        ImageListTextBox.AppendText(currentBatchImageNames);
                                        ImageListTextBox.ScrollToEnd();

                                        // Reset batch variables
                                        currentBatchImageNames = string.Empty;
                                        imagesInCurrentBatch = 0;
                                    }
                                });
                            }
                            else
                            {
                                System.Windows.MessageBox.Show($"Invalid image format: {imageFile}");
                            }
                        }

                        // Add the processed image file to the list
                        lock (processedImageFiles)
                        {
                            processedImageFiles.Add(imageFile);
                        }

                        int currentCompleted = System.Threading.Interlocked.Increment(ref completedImages);
                        processedCount++;

                        if (processedCount % batchSize == 0)
                        {
                            await Task.Delay(1); // Allow other tasks to execute
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Optimization process was canceled
                        throw;
                    }
                }, cancellationTokenSource.Token)); // Pass the CancellationToken to the Task.Run method
            }

            // Wait for all tasks to complete or cancellation is requested
            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                System.Windows.MessageBox.Show("Image optimization paused.");
                return;
            }

            // Append any remaining images in the last batch to the TextBox
            if (!string.IsNullOrEmpty(currentBatchImageNames))
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ImageListTextBox.AppendText(currentBatchImageNames);
                    ImageListTextBox.ScrollToEnd();
                });
            }
            ImageListTextBox.Visibility = Visibility.Visible;

            System.Windows.MessageBox.Show($"Image optimization completed.\nTotal Size Reduction: {totalSizeReduction} bytes");

            // Reset visibility of elements
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                TotalImagesLabel.Visibility = Visibility.Hidden;
                ProgressBar.Visibility = Visibility.Hidden;
                ProgressLabel.Visibility = Visibility.Hidden;
                ImageNameLabel.Visibility = Visibility.Hidden;
                ResumeButton.Visibility = Visibility.Hidden;
                PauseButton.Visibility = Visibility.Hidden;
                ImageListTextBox.Visibility = Visibility.Hidden;
            });
        }

        private void ButtonPause_Click(object sender, RoutedEventArgs e)
        {
            // Store the current progress before pausing
            pausedProgress = ProgressBar.Value;

            // Cancel the optimization process
            if (cancellationTokenSource != null)
                cancellationTokenSource.Cancel();
        }

        private async void ButtonResume_Click(object sender, RoutedEventArgs e)
        {
            // Cancel the current optimization process if it is active
            if (cancellationTokenSource != null)
                cancellationTokenSource.Cancel();

            // Reset the completed images count
            int completedImages = processedImageFiles.Count;

            // Reset the total size reduction
            totalSizeReduction = processedImageFiles.Sum(file => new FileInfo(file).Length);

            // Start the optimization process again
            cancellationTokenSource = new CancellationTokenSource();

            // Update the UI with the paused progress value
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ProgressBar.Value = pausedProgress + ProgressBar.Value;
                ProgressLabel.Content = $"Progress: {pausedProgress:F2}% | Size Reduction: {totalSizeReduction:F2}%";
            });

            ButtonOptimize_ClickAsync(sender, e);
        }

        private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // TODO: Handle progress bar value changed event
        }

        private void SelectedDirectoryTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // TODO: Handle selected directory text changed event
        }

        private void ImageListTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            // TODO: Handle image list text changed event
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedDirectoryTextBox.Text = string.Empty;
        }
    }
}
