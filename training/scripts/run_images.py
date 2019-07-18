import os
import sys
from time import time, strftime
from datetime import date

import components.processing.args_processing as arg
import components.utilities.listbox as listbox
from components.processing.voi_extraction_pipelines import pipeline_subvolume_mean_std
from components.utilities.load_write import find_image_paths

if __name__ == '__main__':
    # Arguments
    choice = 'Isokerays'
    data_path = r'//media/dios/dios2/3DHistoData'
    arguments = arg.return_args(data_path, choice)
    arguments.data_path = r'/media/santeri/Transcend/PTA1272/Isokerays_PTA_Rec'

    # Extract sample list
    samples = os.listdir(arguments.data_path)
    samples.sort()
    if arguments.GUI:
        # Use listbox (Result is saved in listbox.file_list)
        listbox.GetFileSelection(arguments.data_path)
        samples = [samples[i] for i in listbox.file_list]

    # Create log
    os.makedirs(arguments.save_image_path + '/Logs', exist_ok=True)
    os.makedirs(arguments.save_image_path + '/Images', exist_ok=True)
    sys.stdout = open(arguments.save_image_path + '/Logs/' + 'images_log_'
                      + str(date.today()) + str(strftime("-%H-%M")) + '.txt', 'w')

    # Find paths for image stacks
    # file_paths = find_image_paths(arguments.data_path, samples)
    file_paths = [arguments.data_path + '/' + f for f in samples]
    file_paths = [file_paths[13]]
    # Loop for pre-processing samples
    for k in range(len(file_paths)):
        start = time()
        # Initiate pipeline
        #try:
        arguments.data_path = file_paths[k]
        pipeline_subvolume_mean_std(arguments, samples[k], render=arguments.render)
        end = time()
        print('Sample processed in {0} min and {1:.1f} sec.'.format(int((end - start) // 60), (end - start) % 60))
        #except Exception:
            #print('Sample {0} failing. Skipping to next one'.format(samples[k]))
            #continue
    print('Done')